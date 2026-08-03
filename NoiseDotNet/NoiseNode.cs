using System;

namespace NoiseDotNet
{
    /// <summary>
    /// Represents a node a coherent noise evaluation graph.
    /// Use static functions on this class to create noise nodes.
    /// NoiseNode is a readonly class.
    /// </summary>
    public sealed partial class NoiseNode
    {
        public NoiseNodeType Type { get; }

        /// <summary>
        /// ConstantValues[i] = the value of the i-th channel for a constant noise node. Only valid for constant noise nodes, otherwise empty.
        /// </summary>
        public ReadOnlySpan<float> ConstantValues => _constantValues.AsSpan();
        readonly float[] _constantValues;

        /// <summary>
        /// Inputs[i] = the i-th input channel for a noise node. Only valid for noise nodes with inputs, otherwise empty.
        /// </summary>
        public ReadOnlySpan<NoiseScalar> Inputs => _inputs.AsSpan();
        readonly NoiseScalar[] _inputs;

        /// <summary>
        /// Private constructor for a noise node with inputs. Player facing API uses static functions.
        /// </summary>
        public NoiseNode(NoiseNodeType type, params NoiseScalar[] inputs)
        {
            Type = type;
            _inputs = inputs ?? Array.Empty<NoiseScalar>();
            _constantValues = Array.Empty<float>();

            // all errors below should indicate an internal error (none should happen if code is working correctly) and include the NoiseNodeType
            if (type.GetInputCount() != _inputs.Length)
            {
                throw new InvalidOperationException(
                    $"Internal error creating NoiseNode of type {type}: " +
                    $"received {_inputs.Length} input channels, but expected {type.GetInputCount()}.");
            }
        }

        /// <summary>
        /// Private constructor for a constant noise node Player facing API uses static functions.
        /// </summary>
        public NoiseNode(NoiseNodeType type, params float[] constantValues)
        {
            Type = type;
            _inputs = Array.Empty<NoiseScalar>();
            _constantValues = constantValues ?? Array.Empty<float>();

            // all errors below should indicate an internal error (none should happen if code is working correctly) and include the NoiseNodeType
            if (type.GetOutputCount() != _constantValues.Length)
            {
                throw new InvalidOperationException(
                    $"Internal error creating constant NoiseNode of type {type}: " +
                    $"received {_constantValues.Length} constant values, but expected {type.GetOutputCount()}.");
            }
        }

        /// <summary>
        /// Returns whether this node is a constant node.
        /// </summary>
        public bool IsConstant => _constantValues.Length > 0;

        /// <summary>
        /// Returns the number of output channels for this node.
        /// </summary>
        public int OutputChannelCount => Type.GetOutputCount();

        /// <summary>
        /// Returns the number of input channels for this node.
        /// </summary>
        public int InputChannelCount => Type.GetInputCount();

        /// <summary>
        /// Returns a channel reference for a specific output channel of this NoiseNode.
        /// </summary>
        public NoiseScalar Channel(int channelIndex) => new(this, channelIndex);

        /// <summary>
        /// Returns this node's only output as a scalar. Throws if this node does not have exactly one output channel.
        /// </summary>
        public NoiseScalar AsScalar
        {
            get
            {
                ValidateOutputChannelCount(1, nameof(NoiseScalar));
                return Channel(0);
            }
        }

        /// <summary>
        /// Returns this node's outputs as a two-component vector. Throws if this node does not have exactly two output channels.
        /// </summary>
        public NoiseVector2 AsVector2
        {
            get
            {
                ValidateOutputChannelCount(2, nameof(NoiseVector2));
                return new NoiseVector2(Channel(0), Channel(1));
            }
        }

        /// <summary>
        /// Returns this node's outputs as a three-component vector. Throws if this node does not have exactly three output channels.
        /// </summary>
        public NoiseVector3 AsVector3
        {
            get
            {
                ValidateOutputChannelCount(3, nameof(NoiseVector3));
                return new NoiseVector3(Channel(0), Channel(1), Channel(2));
            }
        }

        void ValidateOutputChannelCount(int expectedCount, string targetType)
        {
            if (OutputChannelCount != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Failed to cast NoiseNode to {targetType}: " +
                    $"NoiseNode of type {Type} has {OutputChannelCount} output channels, " +
                    $"but only nodes with {expectedCount} output channels can be cast to a {targetType}. " +
                    "Use NoiseNode.Channel(int) to access a specific channel of a NoiseNode with a different number of output channels.");
            }
        }

    }

    /// <summary>
    /// <para>A scalar noise expression.</para>
    /// </summary>
    public readonly partial struct NoiseScalar : IEquatable<NoiseScalar>
    {
        public readonly NoiseNode Node;
        public readonly int ChannelIndex;

        public NoiseScalar(NoiseNode node, int channelIndex)
        {
            if ((uint)channelIndex >= (uint)node.OutputChannelCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(channelIndex),
                    channelIndex,
                    $"NoiseNode of type {node.Type} has {node.OutputChannelCount} output channels. " +
                    $"Channel index must be between 0 and {node.OutputChannelCount - 1}.");
            }

            Node = node;
            ChannelIndex = channelIndex;
        }

        /// <summary>
        /// Returns true if this channel references a constant NoiseNode.
        /// </summary>
        public bool IsConstant => Node.IsConstant;

        public bool Equals(NoiseScalar other) => Node == other.Node && ChannelIndex == other.ChannelIndex;
        public override bool Equals(object? obj) => obj is NoiseScalar other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Node, ChannelIndex);
        public static bool operator ==(NoiseScalar a, NoiseScalar b) => a.Equals(b);
        public static bool operator !=(NoiseScalar a, NoiseScalar b) => !a.Equals(b);
    }

    /// <summary>
    /// Contains two scalar noise channels.
    /// </summary>
    public readonly partial struct NoiseVector2
    {
        public readonly NoiseScalar X;
        public readonly NoiseScalar Y;

        public NoiseVector2(NoiseScalar x, NoiseScalar y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Contains three scalar noise channels.
    /// </summary>
    public readonly partial struct NoiseVector3
    {
        public readonly NoiseScalar X;
        public readonly NoiseScalar Y;
        public readonly NoiseScalar Z;

        public NoiseVector3(NoiseScalar x, NoiseScalar y, NoiseScalar z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// <para>Type of operation a noise node performs.
    /// All operations have a fixed number of inputs and outputs. All inputs and outputs are floats.</para>
    /// Operation type names are broken into 3 components, seperated by double underscores:
    /// <para>1. The operation name (e.g. "Add")</para>
    /// <para>2. The input channel names, separated by underscores (e.g. "a_b")</para>
    /// <para>3. The output channel names, separated by underscores (e.g. "sum")</para>
    /// </summary>
    public enum NoiseNodeType
    {
        Null,
        Coords1__NoIn__x,
        Coords2__NoIn__x_y,
        Coords3__NoIn__x_y_z,
        Constant1__NoIn__x,
        Constant2__NoIn__x_y,
        Constant3__NoIn__x_y_z,
        Perlin2D_noise__x_y__noise,
        Perlin3D_noise__x_y_z__noise,
        Cellular2_noise__x_y__center_edge,
        Cellular3_noise__x_y_z__center_edge,
        Add__a_b__sum,
        Negate__value__negated,
        Multiply__a_b__product,
        Inverse__value__inverse,
        Min__a_b__min,
        Max__a_b__max,
        Pow__value_power__result,
        SmoothStep01__value__result,
        Lerp__a_b_t__result,
        Floor__value__result,
    }

    /// <summary>
    /// Extention methods for <see cref="NoiseNodeType"/> that allow you to query metadata about the node type.
    /// </summary>
    public static class NoiseNodeTypeExtensions
    {
        /// <summary>
        /// Returns the number of input channels for the given node type.
        /// </summary>
        public static int GetInputCount(this NoiseNodeType type) => GetMetadata(type).InputCount;

        /// <summary>
        /// Returns the number of output channels for the given node type.
        /// </summary>
        public static int GetOutputCount(this NoiseNodeType type) => GetMetadata(type).OutputCount;

        /// <summary>
        /// Returns whether the given node type is a noise node (varies based on seed).
        /// </summary>
        public static bool IsNoise(this NoiseNodeType type) => GetMetadata(type).IsNoise;

        /// <summary>
        /// Returns whether <paramref name="type"/> is a named member of <see cref="NoiseNodeType"/>.
        /// </summary>
        internal static bool IsExecutable(NoiseNodeType type) => TryGetMetadata(type, out _);

        internal readonly struct NoiseNodeTypeMetadata
        {
            public readonly int InputCount;
            public readonly int OutputCount;
            public readonly bool IsNoise;

            public NoiseNodeTypeMetadata(int inputCount, int outputCount, bool isNoise)
            {
                InputCount = inputCount;
                OutputCount = outputCount;
                IsNoise = isNoise;
            }

            public override string ToString() => $"(InputCount: {InputCount}, OutputCount: {OutputCount}, IsNoise: {IsNoise})";
        }

        static NoiseNodeTypeMetadata GetMetadata(NoiseNodeType type)
        {
            if (!TryGetMetadata(type, out NoiseNodeTypeMetadata metadata))
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown NoiseNodeType value.");
            return metadata;
        }

        // This is a switch rather than a lookup table computed at startup because it needs to be
        // callable from Burst compiled code, which cannot read managed caches (arrays, dictionaries,
        // etc.) built at runtime. NoiseNodeTypeMetadataTests keeps this in sync with the
        // NoiseNodeType naming convention by cross-checking every case here against metadata parsed
        // from each enum member's name via ParseMetadataFromName.
        internal static bool TryGetMetadata(NoiseNodeType type, out NoiseNodeTypeMetadata metadata)
        {
            metadata = type switch
            {
                NoiseNodeType.Coords1__NoIn__x => new(0, 1, false),
                NoiseNodeType.Coords2__NoIn__x_y => new(0, 2, false),
                NoiseNodeType.Coords3__NoIn__x_y_z => new(0, 3, false),
                NoiseNodeType.Constant1__NoIn__x => new(0, 1, false),
                NoiseNodeType.Constant2__NoIn__x_y => new(0, 2, false),
                NoiseNodeType.Constant3__NoIn__x_y_z => new(0, 3, false),
                NoiseNodeType.Perlin2D_noise__x_y__noise => new(2, 1, true),
                NoiseNodeType.Perlin3D_noise__x_y_z__noise => new(3, 1, true),
                NoiseNodeType.Cellular2_noise__x_y__center_edge => new(2, 2, true),
                NoiseNodeType.Cellular3_noise__x_y_z__center_edge => new(3, 2, true),
                NoiseNodeType.Add__a_b__sum => new(2, 1, false),
                NoiseNodeType.Negate__value__negated => new(1, 1, false),
                NoiseNodeType.Multiply__a_b__product => new(2, 1, false),
                NoiseNodeType.Inverse__value__inverse => new(1, 1, false),
                NoiseNodeType.Min__a_b__min => new(2, 1, false),
                NoiseNodeType.Max__a_b__max => new(2, 1, false),
                NoiseNodeType.Pow__value_power__result => new(2, 1, false),
                NoiseNodeType.SmoothStep01__value__result => new(1, 1, false),
                NoiseNodeType.Lerp__a_b_t__result => new(3, 1, false),
                NoiseNodeType.Floor__value__result => new(1, 1, false),
                _ => default
            };

            // return true if metadata isn't default
            return metadata.InputCount > 0 | metadata.OutputCount > 0;
        }

        /// <summary>
        /// Parses input/output channel counts and noise-ness from a <see cref="NoiseNodeType"/>
        /// member's name, following the "Operation__Inputs__Outputs" naming convention documented on
        /// <see cref="NoiseNodeType"/>. Used only by tests to verify <see cref="TryGetMetadata"/> (the
        /// switch actually used at runtime) stays in sync with the enum; not used at runtime itself
        /// since it relies on reflection (<see cref="Enum.ToString()"/>), which isn't Burst compatible.
        /// </summary>
        internal static NoiseNodeTypeMetadata ParseMetadataFromName(NoiseNodeType value)
        {
            string[] nameParts = value.ToString().Split("__");
            if (nameParts.Length != 3)
                throw new InvalidOperationException($"NoiseNodeType {value} does not follow the required naming convention.");

            return new NoiseNodeTypeMetadata(
                CountChannels(nameParts[1]),
                CountChannels(nameParts[2]),
                nameParts[0].EndsWith("_noise", StringComparison.Ordinal));

            static int CountChannels(string channels)
            {
                if (channels == "NoIn")
                    return 0;

                int count = 1;
                foreach (char character in channels)
                    if (character == '_')
                        count++;
                return count;
            }
        }
    }
}
