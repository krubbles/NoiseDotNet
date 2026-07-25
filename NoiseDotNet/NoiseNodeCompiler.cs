    /// <summary>
    /// Stateful NoiseNode compiler.
    /// </summary>
    sealed class NoiseNodeCompiler
    {
        readonly NoiseScalar[] _outputs;
        readonly Dictionary<NoiseNode, VisitState> _visitStates = new();
        readonly List<NoiseNode> _discoveryOrder = [];
        readonly Dictionary<NoiseNode, NoiseScalar[]> _effectiveInputs = new();
        readonly Dictionary<ValueKey, int> _remainingUses = new();
        readonly Dictionary<ValueKey, int> _registers = new();
        readonly Dictionary<NoiseNode, int> _noiseSeeds = new();
        readonly Dictionary<NoiseNode, int> _pressureCache = new();
        readonly HashSet<NoiseNode> _compiledNodes = [];
        readonly SortedSet<int> _freeRegisters = [];
        readonly List<float> _constants = [];
        readonly List<byte> _instructions = [];

        int _inputCount;
        int _nextRegister;
        int _maxRegisterCount;

        public NoiseNodeCompiler(NoiseScalar[] outputs)
        {
            _outputs = outputs;
        }

        public CompiledNoiseNode Compile()
        {
            for (int outputIndex = 0; outputIndex < _outputs.Length; outputIndex++)
            {
                NoiseScalar output = _outputs[outputIndex];
                ValidateOutput(output, outputIndex);
                AddUse(new ValueKey(output.Node, output.ChannelIndex));
                Visit(output.Node);
            }

            InitializeLeafRegisters();
            AssignNoiseSeeds();
            CompileDependencies(_outputs);
            MaterializeOutputs();

            ByteCodeInfo info = new()
            {
                InputCount = _inputCount,
                OutputCount = _outputs.Length,
                RegisterCount = _maxRegisterCount,
                ConstantCount = _constants.Count,
            };

            List<byte> bytecode = new(
                Unsafe.SizeOf<ByteCodeInfo>() +
                _constants.Count * sizeof(float) +
                _instructions.Count);
            NoiseNodeByteCode.Append(bytecode, info);
            foreach (float constant in _constants)
                NoiseNodeByteCode.Append(bytecode, constant);
            bytecode.AddRange(_instructions);
            return new CompiledNoiseNode(bytecode.ToArray());
        }

        void Visit(NoiseNode node)
        {
            if (_visitStates.TryGetValue(node, out VisitState state))
            {
                if (state == VisitState.Visiting)
                    throw new InvalidOperationException($"NoiseNode graph contains a cycle at node type {node.Type}.");
                return;
            }

            _visitStates.Add(node, VisitState.Visiting);
            _discoveryOrder.Add(node);

            NoiseScalar[] inputs = GetEffectiveInputs(node);
            foreach (NoiseScalar input in inputs)
            {
                ValidateScalar(input, node);
                AddUse(new ValueKey(input.Node, input.ChannelIndex));
                Visit(input.Node);
            }

            _visitStates[node] = VisitState.Visited;
        }

        NoiseScalar[] GetEffectiveInputs(NoiseNode node)
        {
            if (_effectiveInputs.TryGetValue(node, out NoiseScalar[]? cached))
                return cached;

            NoiseScalar[] inputs = node.Inputs.ToArray();
            if (node.Type.IsNoise())
            {
                for (int i = 0; i < inputs.Length; i++)
                    inputs[i] = RemoveConstantScale(inputs[i], out _);
            }

            _effectiveInputs.Add(node, inputs);
            return inputs;
        }

        NoiseScalar RemoveConstantScale(NoiseScalar input, out float frequency)
        {
            frequency = 1f;
            NoiseScalar current = input;
            while (current.Node.Type == NoiseNodeType.Multiply__a_b__product)
            {
                ReadOnlySpan<NoiseScalar> multiplyInputs = current.Node.Inputs;
                if (TryGetConstant(multiplyInputs[0], out float leftConstant))
                {
                    frequency *= leftConstant;
                    current = multiplyInputs[1];
                }
                else if (TryGetConstant(multiplyInputs[1], out float rightConstant))
                {
                    frequency *= rightConstant;
                    current = multiplyInputs[0];
                }
                else
                {
                    break;
                }
            }
            return current;
        }

        static bool TryGetConstant(NoiseScalar scalar, out float value)
        {
            if (scalar.IsConstant &&
                (uint)scalar.ChannelIndex < (uint)scalar.Node.ConstantValues.Length)
            {
                value = scalar.Node.ConstantValues[scalar.ChannelIndex];
                return true;
            }

            value = default;
            return false;
        }

        void InitializeLeafRegisters()
        {
            foreach (NoiseNode node in _discoveryOrder)
            {
                if (IsCoordinates(node.Type))
                    _inputCount = Math.Max(_inputCount, node.OutputChannelCount);
            }

            foreach (NoiseNode node in _discoveryOrder)
            {
                if (IsCoordinates(node.Type))
                {
                    for (int channel = 0; channel < node.OutputChannelCount; channel++)
                        _registers[new ValueKey(node, channel)] = channel;
                }
            }

            int constantRegister = _inputCount;
            foreach (NoiseNode node in _discoveryOrder)
            {
                if (!node.IsConstant)
                    continue;

                ReadOnlySpan<float> values = node.ConstantValues;
                for (int channel = 0; channel < values.Length; channel++)
                {
                    _registers[new ValueKey(node, channel)] = constantRegister++;
                    _constants.Add(values[channel]);
                }
            }

            _nextRegister = Math.Max(constantRegister, _outputs.Length);
            _maxRegisterCount = _nextRegister;
            for (int register = 0; register < _nextRegister; register++)
            {
                if (!IsRegisterLive(register))
                    _freeRegisters.Add(register);
            }
        }

        void AssignNoiseSeeds()
        {
            int nextSeed = 0;
            foreach (NoiseNode node in _discoveryOrder)
            {
                if (node.Type.IsNoise())
                    _noiseSeeds.Add(node, nextSeed++);
            }
        }

        void CompileNode(NoiseNode node)
        {
            if (_compiledNodes.Contains(node))
                return;

            if (IsCoordinates(node.Type) || node.IsConstant)
            {
                _compiledNodes.Add(node);
                return;
            }

            if (!NoiseNodeByteCode.IsExecutable(node.Type))
                throw new NotSupportedException($"Cannot compile NoiseNodeType {node.Type}.");

            if (TryCompileAccumulatedNoise(node))
                return;

            NoiseScalar[] inputs = GetEffectiveInputs(node);
            CompileDependencies(inputs);

            int[] inputRegisters = GetRegisters(inputs);
            int[] outputRegisters = AllocateOutputs(node, inputs);
            EmitInstruction(node, inputs, inputRegisters, outputRegisters, accumulate: false);

            for (int channel = 0; channel < outputRegisters.Length; channel++)
                _registers[new ValueKey(node, channel)] = outputRegisters[channel];
            _compiledNodes.Add(node);

            Consume(inputs, outputRegisters);
            ReleaseUnusedOutputs(node);
        }

        bool TryCompileAccumulatedNoise(NoiseNode node)
        {
            if (node.Type != NoiseNodeType.Add__a_b__sum || node.OutputChannelCount != 1)
                return false;

            NoiseScalar[] addInputs = GetEffectiveInputs(node);
            NoiseScalar noiseValue;
            NoiseScalar otherValue;
            if (IsSingleOutputNoise(addInputs[0]) && GetRemainingUses(addInputs[0]) == 1)
            {
                noiseValue = addInputs[0];
                otherValue = addInputs[1];
            }
            else if (IsSingleOutputNoise(addInputs[1]) && GetRemainingUses(addInputs[1]) == 1)
            {
                noiseValue = addInputs[1];
                otherValue = addInputs[0];
            }
            else
            {
                return false;
            }

            NoiseNode noiseNode = noiseValue.Node;
            if (_compiledNodes.Contains(noiseNode))
                return false;

            NoiseScalar[] noiseInputs = GetEffectiveInputs(noiseNode);
            NoiseScalar[] dependencies = new NoiseScalar[noiseInputs.Length + 1];
            noiseInputs.CopyTo(dependencies, 0);
            dependencies[^1] = otherValue;
            CompileDependencies(dependencies);

            int otherRegister = _registers[new ValueKey(otherValue.Node, otherValue.ChannelIndex)];
            int outputRegister;
            if (GetRemainingUses(otherValue) == 1 &&
                CanOverwriteRegister(otherRegister, dependencies))
            {
                outputRegister = otherRegister;
            }
            else
            {
                outputRegister = AllocateRegister();
            }

            if (outputRegister != otherRegister)
                NoiseNodeByteCode.AppendCopy(_instructions, otherRegister, outputRegister);

            int[] noiseInputRegisters = GetRegisters(noiseInputs);
            EmitInstruction(
                noiseNode,
                noiseInputs,
                noiseInputRegisters,
                [outputRegister],
                accumulate: true);

            _registers[new ValueKey(noiseNode, 0)] = outputRegister;
            _registers[new ValueKey(node, 0)] = outputRegister;
            _compiledNodes.Add(noiseNode);
            _compiledNodes.Add(node);

            Consume(noiseInputs, [outputRegister]);
            Consume(addInputs, [outputRegister]);
            return true;
        }

        void CompileDependencies(ReadOnlySpan<NoiseScalar> inputs)
        {
            List<(NoiseNode Node, int InputOrder, int Pressure)> dependencies = [];
            HashSet<NoiseNode> seen = [];
            for (int i = 0; i < inputs.Length; i++)
            {
                NoiseNode dependency = inputs[i].Node;
                if (!_compiledNodes.Contains(dependency) && seen.Add(dependency))
                    dependencies.Add((dependency, i, GetPressure(dependency)));
            }

            dependencies.Sort(static (a, b) =>
            {
                int pressureOrder = b.Pressure.CompareTo(a.Pressure);
                return pressureOrder != 0 ? pressureOrder : a.InputOrder.CompareTo(b.InputOrder);
            });

            foreach ((NoiseNode dependency, _, _) in dependencies)
                CompileNode(dependency);
        }

        int GetPressure(NoiseNode node)
        {
            if (_pressureCache.TryGetValue(node, out int pressure))
                return pressure;
            if (IsCoordinates(node.Type) || node.IsConstant)
                return _pressureCache[node] = 0;

            int result = node.OutputChannelCount;
            foreach (NoiseScalar input in GetEffectiveInputs(node))
                result = Math.Max(result, GetPressure(input.Node) + node.OutputChannelCount);
            _pressureCache[node] = result;
            return result;
        }

        int[] AllocateOutputs(NoiseNode node, ReadOnlySpan<NoiseScalar> inputs)
        {
            int[] outputs = new int[node.OutputChannelCount];
            HashSet<int> assigned = [];
            for (int channel = 0; channel < outputs.Length; channel++)
            {
                int register = -1;
                foreach (NoiseScalar input in inputs)
                {
                    int candidate = _registers[new ValueKey(input.Node, input.ChannelIndex)];
                    if (GetRemainingUses(input) == 1 &&
                        !assigned.Contains(candidate) &&
                        CanOverwriteRegister(candidate, inputs))
                    {
                        register = candidate;
                        break;
                    }
                }

                if (register < 0)
                    register = AllocateRegister();
                assigned.Add(register);
                outputs[channel] = register;
            }
            return outputs;
        }

        bool CanOverwriteRegister(int register, ReadOnlySpan<NoiseScalar> consumedValues)
        {
            foreach ((ValueKey value, int valueRegister) in _registers)
            {
                if (valueRegister != register)
                    continue;

                int consumedCount = 0;
                foreach (NoiseScalar consumed in consumedValues)
                {
                    if (ReferenceEquals(value.Node, consumed.Node) && value.Channel == consumed.ChannelIndex)
                        consumedCount++;
                }

                if (GetRemainingUses(value) > consumedCount)
                    return false;
            }
            return true;
        }

        int AllocateRegister()
        {
            if (_freeRegisters.Count > 0)
            {
                int register = _freeRegisters.Min;
                _freeRegisters.Remove(register);
                return register;
            }

            int allocated = _nextRegister++;
            _maxRegisterCount = Math.Max(_maxRegisterCount, _nextRegister);
            return allocated;
        }

        void EmitInstruction(
            NoiseNode node,
            ReadOnlySpan<NoiseScalar> inputs,
            ReadOnlySpan<int> inputRegisters,
            ReadOnlySpan<int> outputRegisters,
            bool accumulate)
        {
            int opCode = (int)node.Type;
            if ((uint)opCode >= byte.MaxValue)
                throw new InvalidOperationException($"NoiseNodeType {node.Type} cannot be represented by the bytecode opcode.");

            NoiseNodeByteCode.Append(_instructions, (byte)opCode);
            if (node.Type.IsNoise())
            {
                NoiseOpInfo noiseInfo = CreateNoiseInfo(node, inputs, accumulate);
                NoiseNodeByteCode.Append(_instructions, noiseInfo);
            }

            foreach (int register in inputRegisters)
                NoiseNodeByteCode.Append(_instructions, register);
            foreach (int register in outputRegisters)
                NoiseNodeByteCode.Append(_instructions, register);
        }

        NoiseOpInfo CreateNoiseInfo(NoiseNode node, ReadOnlySpan<NoiseScalar> effectiveInputs, bool accumulate)
        {
            NoiseOpInfo info = new()
            {
                Accumulate = accumulate,
                XFrequency = 1f,
                YFrequency = 1f,
                ZFrequency = 1f,
                Seed = _noiseSeeds[node],
            };

            ReadOnlySpan<NoiseScalar> originalInputs = node.Inputs;
            for (int i = 0; i < originalInputs.Length; i++)
            {
                NoiseScalar stripped = RemoveConstantScale(originalInputs[i], out float frequency);
                if (stripped != effectiveInputs[i])
                    throw new InvalidOperationException($"Internal error compiling frequency for NoiseNodeType {node.Type}.");

                switch (i)
                {
                    case 0: info.XFrequency = frequency; break;
                    case 1: info.YFrequency = frequency; break;
                    case 2: info.ZFrequency = frequency; break;
                }
            }
            return info;
        }

        int[] GetRegisters(ReadOnlySpan<NoiseScalar> values)
        {
            int[] registers = new int[values.Length];
            for (int i = 0; i < values.Length; i++)
                registers[i] = _registers[new ValueKey(values[i].Node, values[i].ChannelIndex)];
            return registers;
        }

        void Consume(ReadOnlySpan<NoiseScalar> values, ReadOnlySpan<int> protectedRegisters)
        {
            foreach (NoiseScalar value in values)
            {
                ValueKey key = new(value.Node, value.ChannelIndex);
                int remaining = _remainingUses[key] - 1;
                _remainingUses[key] = remaining;
                if (remaining != 0)
                    continue;

                int register = _registers[key];
                if (!protectedRegisters.Contains(register) && !IsRegisterLive(register))
                    _freeRegisters.Add(register);
            }
        }

        void ReleaseUnusedOutputs(NoiseNode node)
        {
            for (int channel = 0; channel < node.OutputChannelCount; channel++)
            {
                ValueKey key = new(node, channel);
                if (GetRemainingUses(key) == 0)
                {
                    int register = _registers[key];
                    if (!IsRegisterLive(register))
                        _freeRegisters.Add(register);
                }
            }
        }

        bool IsRegisterLive(int register)
        {
            foreach ((ValueKey value, int valueRegister) in _registers)
            {
                if (valueRegister == register && GetRemainingUses(value) > 0)
                    return true;
            }
            return false;
        }

        void MaterializeOutputs()
        {
            List<RegisterMove> pending = [];
            for (int outputIndex = 0; outputIndex < _outputs.Length; outputIndex++)
            {
                NoiseScalar output = _outputs[outputIndex];
                int source = _registers[new ValueKey(output.Node, output.ChannelIndex)];
                if (source != outputIndex)
                    pending.Add(new RegisterMove(source, outputIndex));
            }

            while (pending.Count > 0)
            {
                int safeMoveIndex = FindSafeMove(pending);
                if (safeMoveIndex >= 0)
                {
                    RegisterMove move = pending[safeMoveIndex];
                    NoiseNodeByteCode.AppendCopy(_instructions, move.Source, move.Destination);
                    pending.RemoveAt(safeMoveIndex);
                    continue;
                }

                int sourceToPreserve = pending[0].Source;
                int temporary = AllocateRegister();
                NoiseNodeByteCode.AppendCopy(_instructions, sourceToPreserve, temporary);
                for (int i = 0; i < pending.Count; i++)
                {
                    if (pending[i].Source == sourceToPreserve)
                        pending[i] = new RegisterMove(temporary, pending[i].Destination);
                }
            }
        }

        static int FindSafeMove(List<RegisterMove> pending)
        {
            for (int candidateIndex = 0; candidateIndex < pending.Count; candidateIndex++)
            {
                int destination = pending[candidateIndex].Destination;
                bool destinationIsNeeded = false;
                for (int otherIndex = 0; otherIndex < pending.Count; otherIndex++)
                {
                    if (otherIndex != candidateIndex && pending[otherIndex].Source == destination)
                    {
                        destinationIsNeeded = true;
                        break;
                    }
                }

                if (!destinationIsNeeded)
                    return candidateIndex;
            }
            return -1;
        }

        void AddUse(ValueKey key)
        {
            _remainingUses.TryGetValue(key, out int uses);
            _remainingUses[key] = uses + 1;
        }

        int GetRemainingUses(NoiseScalar scalar) =>
            GetRemainingUses(new ValueKey(scalar.Node, scalar.ChannelIndex));

        int GetRemainingUses(ValueKey key) =>
            _remainingUses.TryGetValue(key, out int uses) ? uses : 0;

        static bool IsSingleOutputNoise(NoiseScalar scalar) =>
            scalar.ChannelIndex == 0 &&
            scalar.Node.OutputChannelCount == 1 &&
            scalar.Node.Type.IsNoise();

        static bool IsCoordinates(NoiseNodeType type) => type is
            NoiseNodeType.Coords1__NoIn__x or
            NoiseNodeType.Coords2__NoIn__x_y or
            NoiseNodeType.Coords3__NoIn__x_y_z;

        static void ValidateScalar(NoiseScalar scalar, NoiseNode consumer)
        {
            if (scalar.Node is null)
            {
                throw new InvalidOperationException(
                    $"NoiseNodeType {consumer.Type} contains an input with a null NoiseNode.");
            }
            if ((uint)scalar.ChannelIndex >= (uint)scalar.Node.OutputChannelCount)
            {
                throw new InvalidOperationException(
                    $"NoiseNodeType {consumer.Type} references channel {scalar.ChannelIndex} " +
                    $"of NoiseNodeType {scalar.Node.Type}, which has {scalar.Node.OutputChannelCount} output channels.");
            }
        }

        static void ValidateOutput(NoiseScalar output, int outputIndex)
        {
            if (output.Node is null)
            {
                throw new ArgumentException(
                    $"Output channel at index {outputIndex} contains a null NoiseNode.",
                    "outputs");
            }
            if ((uint)output.ChannelIndex >= (uint)output.Node.OutputChannelCount)
            {
                throw new ArgumentException(
                    $"Output channel at index {outputIndex} references channel {output.ChannelIndex} " +
                    $"of NoiseNodeType {output.Node.Type}, which has {output.Node.OutputChannelCount} output channels.",
                    "outputs");
            }
        }

        enum VisitState : byte
        {
            Visiting,
            Visited,
        }

        readonly struct RegisterMove
        {
            public readonly int Source;
            public readonly int Destination;

            public RegisterMove(int source, int destination)
            {
                Source = source;
                Destination = destination;
            }
        }

        readonly struct ValueKey : IEquatable<ValueKey>
        {
            public readonly NoiseNode Node;
            public readonly int Channel;

            public ValueKey(NoiseNode node, int channel)
            {
                Node = node;
                Channel = channel;
            }

            public bool Equals(ValueKey other) =>
                ReferenceEquals(Node, other.Node) && Channel == other.Channel;

            public override bool Equals(object? obj) => obj is ValueKey other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(RuntimeHelpers.GetHashCode(Node), Channel);
        }
    }
