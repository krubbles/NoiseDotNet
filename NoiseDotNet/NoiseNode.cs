using System;

public sealed class NoiseNode
{
    public NoiseNodeType Type { get; }


    readonly float[] _constantValues;

    /// <summary>
    /// ConstantValues[i] = the value of the i-th channel for a constant noise node. Only valid for constant noise nodes, otherwise empty.
    /// </summary>
    public ReadOnlySpan<float> ConstantValues => _constantValues.AsSpan();

    readonly NoiseScalar[] _inputs;

    /// <summary>
    /// Inputs[i] = the i-th input channel for a noise node. Only valid for noise nodes with inputs, otherwise empty.
    /// </summary>
    public ReadOnlySpan<NoiseScalar> Inputs => _inputs.AsSpan();

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
        _inputs = [];
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

    /// <summary>
    /// Checks if a number is real, throwing an exception if it is infinity or NaN.
    /// </summary>
    public static float ValidateIsRealNumber(string message, string varName, float value)
    {
        // its pretty easy to accidently generate NaN from DB0 error and hard to debug if we don't catch it here.
        if (float.IsInfinity(value) || float.IsNaN(value))
            throw new NotFiniteNumberException($"{message}. {varName} = {value}");
        return value;
    }

    const string _constantNotRealErrorMessage = "Failed to create constant NoiseNode because constant was not a real number. ";


    /// <summary>
    /// Creates a one-channel constant node.
    /// </summary>
    public static NoiseScalar Constant(float x) => new NoiseNode(NoiseNodeType.Constant1__NoIn__x,
        ValidateIsRealNumber(_constantNotRealErrorMessage, "x", x)).AsScalar;

    /// <summary>
    /// Creates a two-channel constant node.
    /// </summary>
    public static NoiseVector2 Constant(float x, float y) => new NoiseNode(NoiseNodeType.Constant2__NoIn__x_y,
        ValidateIsRealNumber(_constantNotRealErrorMessage, "x", x),
        ValidateIsRealNumber(_constantNotRealErrorMessage, "y", y)).AsVector2;

    /// <summary>
    /// Creates a three-channel constant node.
    /// </summary>
    public static NoiseVector3 Constant(float x, float y, float z) => new NoiseNode(NoiseNodeType.Constant3__NoIn__x_y_z,
        ValidateIsRealNumber(_constantNotRealErrorMessage, "x", x),
        ValidateIsRealNumber(_constantNotRealErrorMessage, "y", y),
        ValidateIsRealNumber(_constantNotRealErrorMessage, "z", z)).AsVector3;


    static NoiseNode InlineConstantCommunative(NoiseNode node)
    {
        NoiseScalar left = node._inputs[0];
        NoiseScalar right = node._inputs[1];

        if (left.IsConstant && right.IsConstant)
            return InlineConstant(node);

        bool leftHasConstant = TrySplitCommutativeOperand(left, node.Type, out NoiseScalar leftValue, out NoiseScalar leftConstant);
        bool rightHasConstant = TrySplitCommutativeOperand(right, node.Type, out NoiseScalar rightValue, out NoiseScalar rightConstant);

        if (leftHasConstant && rightHasConstant)
        {
            NoiseScalar values = new NoiseNode(node.Type, leftValue, rightValue).AsScalar;
            NoiseScalar constants = InlineConstant(new NoiseNode(node.Type, leftConstant, rightConstant)).AsScalar;
            return new NoiseNode(node.Type, values, constants);
        }

        if (left.IsConstant && rightHasConstant)
        {
            NoiseScalar constants = InlineConstant(new NoiseNode(node.Type, left, rightConstant)).AsScalar;
            return new NoiseNode(node.Type, rightValue, constants);
        }

        if (right.IsConstant && leftHasConstant)
        {
            NoiseScalar constants = InlineConstant(new NoiseNode(node.Type, leftConstant, right)).AsScalar;
            return new NoiseNode(node.Type, leftValue, constants);
        }

        return node;

        static bool TrySplitCommutativeOperand(
            NoiseScalar operand,
            NoiseNodeType operatorType,
            out NoiseScalar value,
            out NoiseScalar constant)
        {
            value = default;
            constant = default;

            NoiseNode operandNode = operand.Node;
            if (operandNode.Type != operatorType || operandNode._inputs.Length != 2)
                return false;

            NoiseScalar left = operandNode._inputs[0];
            NoiseScalar right = operandNode._inputs[1];
            if (left.IsConstant == right.IsConstant)
                return false;

            value = left.IsConstant ? right : left;
            constant = left.IsConstant ? left : right;
            return true;
        }
    }

    static NoiseNode InlineConstant(NoiseNode node)
    {
        foreach (NoiseScalar input in node._inputs)
            if (!input.IsConstant)
                return node;

        return node;
    }

    public static NoiseScalar Add(NoiseScalar a, NoiseScalar b)
    {
        NoiseNode result = new(NoiseNodeType.Add__a_b__sum, a, b);
        result = InlineConstantCommunative(result);
        return result.AsScalar;
    }

    public static NoiseVector2 Add(NoiseVector2 a, NoiseVector2 b) => new(
        Add(a.X, b.X),
        Add(a.Y, b.Y));

    public static NoiseVector3 Add(NoiseVector3 a, NoiseVector3 b) => new(
        Add(a.X, b.X),
        Add(a.Y, b.Y),
        Add(a.Z, b.Z));
}

/// <summary>
/// <para>References a specific channel from the output of a noise node.</para>
/// <para>Supports implicit casts from NoiseNode (accessing channel 0) and a (NoiseNode node, int channelIndex) tuple.</para>
/// </summary>
public readonly struct NoiseScalar : IEquatable<NoiseScalar>
{
    public readonly NoiseNode Node;
    public readonly int ChannelIndex;

    public NoiseScalar(NoiseNode node, int channelIndex)
    {
        Node = node;
        ChannelIndex = channelIndex;
    }

    /// <summary>
    /// Returns true if this channel references a constant NoiseNode.
    /// </summary>
    public bool IsConstant => Node.IsConstant;

    public static explicit operator NoiseScalar(NoiseNode node)
    {

        return new NoiseScalar(node, 0);
    }
    public static implicit operator NoiseScalar((NoiseNode node, int channelIndex) pair) => new(pair.node, pair.channelIndex);

    public bool Equals(NoiseScalar other) => Node == other.Node && ChannelIndex == other.ChannelIndex;
    public override bool Equals(object? obj) => obj is NoiseScalar other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Node, ChannelIndex);
    public static bool operator ==(NoiseScalar a, NoiseScalar b) => a.Equals(b);
    public static bool operator !=(NoiseScalar a, NoiseScalar b) => !a.Equals(b);
}

/// <summary>
/// Contains two scalar noise channels.
/// </summary>
public readonly struct NoiseVector2
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
public readonly struct NoiseVector3
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
    Add__a_b__sum,
    Perlin2D__x_y__noise,
    Perlin3D__x_y_z__noise,
    Cellular2__x_y__center_edge,
    Cellular3__x_y_z__center_edge,
}

public static class NoiseNodeTypeExtensions
{
    readonly struct NoiseNodeTypeMetadata
    {
        public readonly int InputCount;
        public readonly int OutputCount;

        public NoiseNodeTypeMetadata(int inputCount, int outputCount)
        {
            InputCount = inputCount;
            OutputCount = outputCount;
        }
    }

    static readonly NoiseNodeTypeMetadata[] _metadata = CreateMetadataCache();

    public static int GetInputCount(this NoiseNodeType type) => GetMetadata(type).InputCount;
    public static int GetOutputCount(this NoiseNodeType type) => GetMetadata(type).OutputCount;

    static ref NoiseNodeTypeMetadata GetMetadata(NoiseNodeType type)
    {
        int index = (int)type;
        if ((uint)index >= (uint)_metadata.Length)
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown NoiseNodeType value.");
        return ref _metadata[index];
    }

    static NoiseNodeTypeMetadata[] CreateMetadataCache()
    {
        NoiseNodeType[] values = Enum.GetValues<NoiseNodeType>();
        int maxValue = 0;
        foreach (NoiseNodeType value in values)
            maxValue = Math.Max(maxValue, (int)value);

        NoiseNodeTypeMetadata[] metadata = new NoiseNodeTypeMetadata[maxValue + 1];
        foreach (NoiseNodeType value in values)
        {
            if (value == NoiseNodeType.Null)
                continue;

            string[] nameParts = value.ToString().Split("__");
            if (nameParts.Length != 3)
                throw new InvalidOperationException($"NoiseNodeType {value} does not follow the required naming convention.");

            metadata[(int)value] = new NoiseNodeTypeMetadata(
                CountChannels(nameParts[1]),
                CountChannels(nameParts[2]));
        }
        return metadata;
    }

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
