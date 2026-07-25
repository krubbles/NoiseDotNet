using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace NoiseDotNet
{
    /// <summary>
    /// Compilation and evaluation utilities for NoiseNode bytecode.
    /// </summary>
    public static class NoiseNodeByteCode
    {
        const byte CopyOpCode = byte.MaxValue;

        /// <summary>
        /// Evaluates compiled NoiseNode bytecode over a batch of values.
        /// The first input registers must already be populated in <paramref name="registerSpace"/>.
        /// On return, the first output registers contain the result. Each register occupies one
        /// contiguous <paramref name="batchSize"/>-element section of <paramref name="registerSpace"/>.
        /// </summary>
        /// <param name="bytecode">Bytecode produced by <see cref="Compile"/>.</param>
        /// <param name="seed">Evaluation seed combined with each compiled noise operation's seed.</param>
        /// <param name="registerSpace">Storage for all input, temporary, constant, and output registers.</param>
        /// <param name="batchSize">Number of values evaluated in each register.</param>
        public static void Evaluate(ReadOnlySpan<byte> bytecode, int seed, Span<float> registerSpace, int batchSize)
        {
            if (batchSize < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(batchSize),
                    batchSize,
                    "Batch size cannot be negative.");
            }

            int offset = 0;
            ByteCodeInfo info = Read<ByteCodeInfo>(bytecode, ref offset);
            ValidateByteCodeInfo(info);

            long requiredRegisterSpaceLong = (long)info.RegisterCount * batchSize;
            if (requiredRegisterSpaceLong > int.MaxValue)
            {
                throw new ArgumentException(
                    $"The compiled graph requires {info.RegisterCount} registers and the requested batch size is {batchSize}, " +
                    $"which would require {requiredRegisterSpaceLong} float values. A Span can contain at most {int.MaxValue} values; " +
                    "use a smaller batch size or compile a graph that requires fewer registers.",
                    nameof(batchSize));
            }
            int requiredRegisterSpace = (int)requiredRegisterSpaceLong;

            if (registerSpace.Length < requiredRegisterSpace)
            {
                throw new ArgumentException(
                    $"Register space contains {registerSpace.Length} values, but the bytecode requires at least {requiredRegisterSpace}.",
                    nameof(registerSpace));
            }

            // init constants
            for (int constantIndex = 0; constantIndex < info.ConstantCount; constantIndex++)
            {
                float value = Read<float>(bytecode, ref offset);
                GetRegister(registerSpace, info.InputCount + constantIndex, batchSize).Fill(value);
            }

            Span<int> inputScratch = stackalloc int[8];
            Span<int> outputScratch = stackalloc int[8];
            while (offset < bytecode.Length)
            {
                byte opCode = Read<byte>(bytecode, ref offset);
                if (opCode == CopyOpCode)
                {
                    int source = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                    int destination = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                    GetRegister(registerSpace, source, batchSize).CopyTo(
                        GetRegister(registerSpace, destination, batchSize));
                    continue;
                }

                NoiseNodeType type = (NoiseNodeType)opCode;
                if (!IsExecutable(type))
                    throw new ArgumentException($"Bytecode contains unsupported opcode {opCode}.", nameof(bytecode));

                NoiseOpInfo noiseInfo = default;
                if (type.IsNoise())
                    noiseInfo = Read<NoiseOpInfo>(bytecode, ref offset);

                int inputCount = type.GetInputCount();
                int outputCount = type.GetOutputCount();
                Span<int> inputs = inputCount <= inputScratch.Length
                    ? inputScratch[..inputCount]
                    : new int[inputCount];
                Span<int> outputs = outputCount <= outputScratch.Length
                    ? outputScratch[..outputCount]
                    : new int[outputCount];

                for (int i = 0; i < inputs.Length; i++)
                    inputs[i] = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                for (int i = 0; i < outputs.Length; i++)
                    outputs[i] = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);

                EvaluateInstruction(type, noiseInfo, seed, registerSpace, batchSize, inputs, outputs);
            }
        }

        /// <summary>
        /// Compiles the graphs needed by the ordered output channels into reusable bytecode.
        /// </summary>
        /// <param name="outputs">Output channels, in the order they should appear in the output registers.</param>
        public static CompiledNoiseNode Compile(params NoiseScalar[] outputs)
        {
            ArgumentNullException.ThrowIfNull(outputs);
            if (outputs.Length == 0)
                throw new ArgumentException("At least one output channel must be provided.", nameof(outputs));
            return new NoiseNodeCompiler((NoiseScalar[])outputs.Clone()).Compile();
        }

        /// <summary>
        /// Returns a human-readable disassembly of compiled NoiseNode bytecode.
        /// </summary>
        public static string ToString(ReadOnlySpan<byte> bytecode)
        {
            int offset = 0;
            ByteCodeInfo info = Read<ByteCodeInfo>(bytecode, ref offset);
            ValidateByteCodeInfo(info);

            StringBuilder result = new();
            result.Append("ByteCodeInfo { Inputs = ")
                .Append(info.InputCount)
                .Append(", Outputs = ")
                .Append(info.OutputCount)
                .Append(", Registers = ")
                .Append(info.RegisterCount)
                .Append(", Constants = ")
                .Append(info.ConstantCount)
                .AppendLine(" }");

            if (info.ConstantCount > 0)
            {
                result.AppendLine("Constants:");
                for (int constantIndex = 0; constantIndex < info.ConstantCount; constantIndex++)
                {
                    float value = Read<float>(bytecode, ref offset);
                    result.Append("  r")
                        .Append(info.InputCount + constantIndex)
                        .Append(" = ")
                        .AppendLine(value.ToString("R", CultureInfo.InvariantCulture));
                }
            }

            result.AppendLine("Instructions:");
            int instructionIndex = 0;
            while (offset < bytecode.Length)
            {
                byte opCode = Read<byte>(bytecode, ref offset);
                result.Append("  ").Append(instructionIndex++).Append(": ");

                if (opCode == CopyOpCode)
                {
                    int source = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                    int destination = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                    result.Append("Copy r").Append(source).Append(" -> r").AppendLine(destination.ToString());
                    continue;
                }

                NoiseNodeType type = (NoiseNodeType)opCode;
                if (!IsExecutable(type))
                    throw new ArgumentException($"Bytecode contains unsupported opcode {opCode}.", nameof(bytecode));

                result.Append(type);
                if (type.IsNoise())
                {
                    NoiseOpInfo noiseInfo = Read<NoiseOpInfo>(bytecode, ref offset);
                    result.Append(" [frequency = (")
                        .Append(noiseInfo.XFrequency.ToString("R", CultureInfo.InvariantCulture))
                        .Append(", ")
                        .Append(noiseInfo.YFrequency.ToString("R", CultureInfo.InvariantCulture))
                        .Append(", ")
                        .Append(noiseInfo.ZFrequency.ToString("R", CultureInfo.InvariantCulture))
                        .Append("), seed = ")
                        .Append(noiseInfo.Seed);
                    if (noiseInfo.Accumulate)
                        result.Append(", accumulate");
                    result.Append(']');
                }

                int inputCount = type.GetInputCount();
                int outputCount = type.GetOutputCount();
                result.Append(" (");
                for (int i = 0; i < inputCount; i++)
                {
                    if (i > 0)
                        result.Append(", ");
                    result.Append('r').Append(ReadRegisterIndex(bytecode, ref offset, info.RegisterCount));
                }
                result.Append(") -> (");
                for (int i = 0; i < outputCount; i++)
                {
                    if (i > 0)
                        result.Append(", ");
                    result.Append('r').Append(ReadRegisterIndex(bytecode, ref offset, info.RegisterCount));
                }
                result.AppendLine(")");
            }

            return result.ToString().TrimEnd();
        }

        static void EvaluateInstruction(
            NoiseNodeType type,
            NoiseOpInfo noiseInfo,
            int evaluationSeed,
            Span<float> registerSpace,
            int batchSize,
            ReadOnlySpan<int> inputs,
            ReadOnlySpan<int> outputs)
        {
            Span<float> output0 = GetRegister(registerSpace, outputs[0], batchSize);

            switch (type)
            {
                case NoiseNodeType.Add__a_b__sum:
                    {
                        Span<float> a = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> b = GetRegister(registerSpace, inputs[1], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = a[i] + b[i];
                        break;
                    }
                case NoiseNodeType.Negate__value__negated:
                    {
                        Span<float> value = GetRegister(registerSpace, inputs[0], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = -value[i];
                        break;
                    }
                case NoiseNodeType.Multiply__a_b__product:
                    {
                        Span<float> a = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> b = GetRegister(registerSpace, inputs[1], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = a[i] * b[i];
                        break;
                    }
                case NoiseNodeType.Inverse__value__inverse:
                    {
                        Span<float> value = GetRegister(registerSpace, inputs[0], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = 1f / value[i];
                        break;
                    }
                case NoiseNodeType.Perlin2D_noise__x_y__noise:
                    Noise.GradientNoise2D(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        output0,
                        CreateNoiseSettings(noiseInfo, evaluationSeed));
                    break;
                case NoiseNodeType.Perlin3D_noise__x_y_z__noise:
                    Noise.GradientNoise3D(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        GetRegister(registerSpace, inputs[2], batchSize),
                        output0,
                        CreateNoiseSettings(noiseInfo, evaluationSeed));
                    break;
                case NoiseNodeType.Cellular2_noise__x_y__center_edge:
                    Noise.CellularNoise2D(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        output0,
                        GetRegister(registerSpace, outputs[1], batchSize),
                        CreateNoiseSettings(noiseInfo, evaluationSeed));
                    break;
                case NoiseNodeType.Cellular3_noise__x_y_z__center_edge:
                    Noise.CellularNoise3D(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        GetRegister(registerSpace, inputs[2], batchSize),
                        output0,
                        GetRegister(registerSpace, outputs[1], batchSize),
                        CreateNoiseSettings(noiseInfo, evaluationSeed));
                    break;
                default:
                    throw new ArgumentException($"Unsupported executable NoiseNodeType {type}.");
            }
        }

        static NoiseSettings CreateNoiseSettings(NoiseOpInfo info, int evaluationSeed) => new(
            xFreq: info.XFrequency,
            yFreq: info.YFrequency,
            zFreq: info.ZFrequency,
            amplitude: 1f,
            amplitude2: 1f,
            seed: evaluationSeed ^ info.Seed,
            accumulate: info.Accumulate);

        static Span<float> GetRegister(Span<float> registerSpace, int register, int batchSize) =>
            registerSpace.Slice(checked(register * batchSize), batchSize);

        static int ReadRegisterIndex(ReadOnlySpan<byte> bytecode, ref int offset, int registerCount)
        {
            int register = Read<int>(bytecode, ref offset);
            if ((uint)register >= (uint)registerCount)
                throw new ArgumentException($"Bytecode references invalid register {register}.", nameof(bytecode));
            return register;
        }

        static T Read<T>(ReadOnlySpan<byte> bytecode, ref int offset) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            if (offset < 0 || bytecode.Length - offset < size)
                throw new ArgumentException("Bytecode ended in the middle of an instruction.", nameof(bytecode));

            T value = MemoryMarshal.Read<T>(bytecode.Slice(offset, size));
            offset += size;
            return value;
        }

        static void ValidateByteCodeInfo(ByteCodeInfo info)
        {
            if (info.InputCount < 0 ||
                info.OutputCount < 0 ||
                info.RegisterCount < 0 ||
                info.ConstantCount < 0)
            {
                throw new ArgumentException("Bytecode contains an invalid header.");
            }

            int fixedRegisterCount;
            try
            {
                fixedRegisterCount = checked(info.InputCount + info.ConstantCount);
            }
            catch (OverflowException exception)
            {
                throw new ArgumentException("Bytecode contains an invalid header.", exception);
            }

            if (info.RegisterCount < Math.Max(fixedRegisterCount, info.OutputCount))
                throw new ArgumentException("Bytecode contains an invalid header.");
        }

        internal static bool IsExecutable(NoiseNodeType type) =>
            type != NoiseNodeType.Null && Enum.IsDefined(type);

        internal static void Append<T>(List<byte> bytecode, T value) where T : unmanaged
        {
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(
                MemoryMarshal.CreateReadOnlySpan(ref value, 1));
            for (int i = 0; i < bytes.Length; i++)
                bytecode.Add(bytes[i]);
        }

        internal static void AppendCopy(List<byte> bytecode, int source, int destination)
        {
            Append(bytecode, CopyOpCode);
            Append(bytecode, source);
            Append(bytecode, destination);
        }
    }

    /// <summary>
    /// Stateful compiler for a set of NoiseNode output channels.
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

    /// <summary>
    /// Information about a compiled NoiseNode. Stored at the beginning of the compiled bytecode.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ByteCodeInfo
    {
        public int InputCount;
        public int OutputCount;
        public int RegisterCount;
        public int ConstantCount;
    }

    /// <summary>
    /// Extra information for a noise-function instruction.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NoiseOpInfo
    {
        /// <summary>
        /// If true, all outputs of the noise function are accumulated instead of overwritten.
        /// </summary>
        public bool Accumulate;
        public float XFrequency;
        public float YFrequency;
        public float ZFrequency;
        public int Seed;
    }

    /// <summary>
    /// A NoiseNode graph compiled into evaluatable bytecode.
    /// </summary>
    public readonly struct CompiledNoiseNode
    {
        /// <summary>
        /// The raw bytecode. Its layout is:
        /// [ByteCodeInfo][constants][[opcode][optional NoiseOpInfo][input registers][output registers]]...
        /// Inputs occupy the first registers, constants the following registers, and final outputs the first registers.
        /// </summary>
        public readonly byte[] ByteCode;

        internal CompiledNoiseNode(byte[] byteCode)
        {
            ByteCode = byteCode;
        }

        /// <summary>
        /// Returns a human-readable disassembly of this compiled graph.
        /// </summary>
        public override string ToString() =>
            ByteCode is null ? "Uninitialized CompiledNoiseNode" : NoiseNodeByteCode.ToString(ByteCode);
    }
}
