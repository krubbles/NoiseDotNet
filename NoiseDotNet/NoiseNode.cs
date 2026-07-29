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

    /// <summary>
    /// Returns the smaller of two scalar values.
    /// </summary>
    public static NoiseScalar Min(NoiseScalar a, NoiseScalar b)
    {
        NoiseNode result = new(NoiseNodeType.Min__a_b__min, a, b);
        result = InlineConstantCommunative(result);
        return result.AsScalar;
    }

    /// <summary>
    /// Returns the component-wise minimum of two vectors.
    /// </summary>
    public static NoiseVector2 Min(NoiseVector2 a, NoiseVector2 b) => new(
        Min(a.X, b.X),
        Min(a.Y, b.Y));

    /// <summary>
    /// Returns the component-wise minimum of two vectors.
    /// </summary>
    public static NoiseVector3 Min(NoiseVector3 a, NoiseVector3 b) => new(
        Min(a.X, b.X),
        Min(a.Y, b.Y),
        Min(a.Z, b.Z));

    /// <summary>
    /// Returns the larger of two scalar values.
    /// </summary>
    public static NoiseScalar Max(NoiseScalar a, NoiseScalar b)
    {
        NoiseNode result = new(NoiseNodeType.Max__a_b__max, a, b);
        result = InlineConstantCommunative(result);
        return result.AsScalar;
    }

    /// <summary>
    /// Returns the component-wise maximum of two vectors.
    /// </summary>
    public static NoiseVector2 Max(NoiseVector2 a, NoiseVector2 b) => new(
        Max(a.X, b.X),
        Max(a.Y, b.Y));

    /// <summary>
    /// Returns the component-wise maximum of two vectors.
    /// </summary>
    public static NoiseVector3 Max(NoiseVector3 a, NoiseVector3 b) => new(
        Max(a.X, b.X),
        Max(a.Y, b.Y),
        Max(a.Z, b.Z));

    /// <summary>
    /// Raises a scalar value to a scalar power.
    /// </summary>
    public static NoiseScalar Pow(NoiseScalar value, NoiseScalar power) =>
        new NoiseNode(NoiseNodeType.Pow__value_power__result, value, power).AsScalar;

    /// <summary>
    /// Raises each component of a vector to the corresponding component of another vector.
    /// </summary>
    public static NoiseVector2 Pow(NoiseVector2 value, NoiseVector2 power) => new(
        Pow(value.X, power.X),
        Pow(value.Y, power.Y));

    /// <summary>
    /// Raises each component of a vector to the corresponding component of another vector.
    /// </summary>
    public static NoiseVector3 Pow(NoiseVector3 value, NoiseVector3 power) => new(
        Pow(value.X, power.X),
        Pow(value.Y, power.Y),
        Pow(value.Z, power.Z));

    /// <summary>
    /// Raises each component of a vector to the same scalar power.
    /// </summary>
    public static NoiseVector2 Pow(NoiseVector2 value, NoiseScalar power) => new(
        Pow(value.X, power),
        Pow(value.Y, power));

    /// <summary>
    /// Raises each component of a vector to the same scalar power.
    /// </summary>
    public static NoiseVector3 Pow(NoiseVector3 value, NoiseScalar power) => new(
        Pow(value.X, power),
        Pow(value.Y, power),
        Pow(value.Z, power));

    public static NoiseScalar Negate(NoiseScalar value) =>
        new NoiseNode(NoiseNodeType.Negate__value__negated, value).AsScalar;

    public static NoiseVector2 Negate(NoiseVector2 value) => new(
        Negate(value.X),
        Negate(value.Y));

    public static NoiseVector3 Negate(NoiseVector3 value) => new(
        Negate(value.X),
        Negate(value.Y),
        Negate(value.Z));

    public static NoiseScalar Subtract(NoiseScalar a, NoiseScalar b) => Add(a, Negate(b));

    public static NoiseVector2 Subtract(NoiseVector2 a, NoiseVector2 b) => Add(a, Negate(b));

    public static NoiseVector3 Subtract(NoiseVector3 a, NoiseVector3 b) => Add(a, Negate(b));

    public static NoiseScalar Multiply(NoiseScalar a, NoiseScalar b)
    {
        NoiseNode result = new(NoiseNodeType.Multiply__a_b__product, a, b);
        result = InlineConstantCommunative(result);
        return result.AsScalar;
    }

    public static NoiseVector2 Multiply(NoiseVector2 a, NoiseVector2 b) => new(
        Multiply(a.X, b.X),
        Multiply(a.Y, b.Y));

    public static NoiseVector3 Multiply(NoiseVector3 a, NoiseVector3 b) => new(
        Multiply(a.X, b.X),
        Multiply(a.Y, b.Y),
        Multiply(a.Z, b.Z));

    public static NoiseVector2 Multiply(NoiseVector2 vector, NoiseScalar scalar) => new(
        Multiply(vector.X, scalar),
        Multiply(vector.Y, scalar));

    public static NoiseVector3 Multiply(NoiseVector3 vector, NoiseScalar scalar) => new(
        Multiply(vector.X, scalar),
        Multiply(vector.Y, scalar),
        Multiply(vector.Z, scalar));

    public static NoiseScalar Inverse(NoiseScalar value) =>
        new NoiseNode(NoiseNodeType.Inverse__value__inverse, value).AsScalar;

    public static NoiseVector2 Inverse(NoiseVector2 value) => new(
        Inverse(value.X),
        Inverse(value.Y));

    public static NoiseVector3 Inverse(NoiseVector3 value) => new(
        Inverse(value.X),
        Inverse(value.Y),
        Inverse(value.Z));

    public static NoiseScalar Divide(NoiseScalar a, NoiseScalar b) => Multiply(a, Inverse(b));

    public static NoiseVector2 Divide(NoiseVector2 a, NoiseVector2 b) => Multiply(a, Inverse(b));

    public static NoiseVector3 Divide(NoiseVector3 a, NoiseVector3 b) => Multiply(a, Inverse(b));

    public static NoiseVector2 Divide(NoiseVector2 vector, NoiseScalar scalar) =>
        Multiply(vector, Inverse(scalar));

    public static NoiseVector3 Divide(NoiseVector3 vector, NoiseScalar scalar) =>
        Multiply(vector, Inverse(scalar));
}

/// <summary>
/// <para>References a specific channel from the output of a noise node.</para>
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

    public static implicit operator NoiseScalar((NoiseNode node, int channelIndex) pair) => new(pair.node, pair.channelIndex);

    public bool Equals(NoiseScalar other) => Node == other.Node && ChannelIndex == other.ChannelIndex;
    public override bool Equals(object? obj) => obj is NoiseScalar other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Node, ChannelIndex);
    public static NoiseScalar operator +(NoiseScalar a, NoiseScalar b) => NoiseNode.Add(a, b);
    public static NoiseScalar operator -(NoiseScalar value) => NoiseNode.Negate(value);
    public static NoiseScalar operator -(NoiseScalar a, NoiseScalar b) => NoiseNode.Subtract(a, b);
    public static NoiseScalar operator *(NoiseScalar a, NoiseScalar b) => NoiseNode.Multiply(a, b);
    public static NoiseScalar operator /(NoiseScalar a, NoiseScalar b) => NoiseNode.Divide(a, b);
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

    public static NoiseVector2 operator +(NoiseVector2 a, NoiseVector2 b) => NoiseNode.Add(a, b);
    public static NoiseVector2 operator -(NoiseVector2 value) => NoiseNode.Negate(value);
    public static NoiseVector2 operator -(NoiseVector2 a, NoiseVector2 b) => NoiseNode.Subtract(a, b);
    public static NoiseVector2 operator *(NoiseVector2 a, NoiseVector2 b) => NoiseNode.Multiply(a, b);
    public static NoiseVector2 operator *(NoiseVector2 vector, NoiseScalar scalar) => NoiseNode.Multiply(vector, scalar);
    public static NoiseVector2 operator *(NoiseScalar scalar, NoiseVector2 vector) => NoiseNode.Multiply(vector, scalar);
    public static NoiseVector2 operator /(NoiseVector2 a, NoiseVector2 b) => NoiseNode.Divide(a, b);
    public static NoiseVector2 operator /(NoiseVector2 vector, NoiseScalar scalar) => NoiseNode.Divide(vector, scalar);
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

    public static NoiseVector3 operator +(NoiseVector3 a, NoiseVector3 b) => NoiseNode.Add(a, b);
    public static NoiseVector3 operator -(NoiseVector3 value) => NoiseNode.Negate(value);
    public static NoiseVector3 operator -(NoiseVector3 a, NoiseVector3 b) => NoiseNode.Subtract(a, b);
    public static NoiseVector3 operator *(NoiseVector3 a, NoiseVector3 b) => NoiseNode.Multiply(a, b);
    public static NoiseVector3 operator *(NoiseVector3 vector, NoiseScalar scalar) => NoiseNode.Multiply(vector, scalar);
    public static NoiseVector3 operator *(NoiseScalar scalar, NoiseVector3 vector) => NoiseNode.Multiply(vector, scalar);
    public static NoiseVector3 operator /(NoiseVector3 a, NoiseVector3 b) => NoiseNode.Divide(a, b);
    public static NoiseVector3 operator /(NoiseVector3 vector, NoiseScalar scalar) => NoiseNode.Divide(vector, scalar);
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
    Negate__value__negated,
    Multiply__a_b__product,
    Inverse__value__inverse,
    Perlin2D_noise__x_y__noise,
    Perlin3D_noise__x_y_z__noise,
    Cellular2_noise__x_y__center_edge,
    Cellular3_noise__x_y_z__center_edge,
    Min__a_b__min,
    Max__a_b__max,
    Pow__value_power__result,
}

public static class NoiseNodeTypeExtensions
{
    readonly struct NoiseNodeTypeMetadata
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
    }

    static readonly NoiseNodeTypeMetadata[] _metadata = CreateMetadataCache();

    public static int GetInputCount(this NoiseNodeType type) => GetMetadata(type).InputCount;
    public static int GetOutputCount(this NoiseNodeType type) => GetMetadata(type).OutputCount;
    public static bool IsNoise(this NoiseNodeType type) => GetMetadata(type).IsNoise;

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
                CountChannels(nameParts[2]),
                nameParts[0].EndsWith("_noise", StringComparison.Ordinal));
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
