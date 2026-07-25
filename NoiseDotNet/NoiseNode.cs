using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Threading.Channels;

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
            // AI throw error w/ actual and expected input counts
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
            // AI throw error w/ actual and expected input counts
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

    public NoiseScalar AsScalar
    {
        get
        {
            if (OutputChannelCount != 1)
            {
                throw new InvalidOperationException(
                    $"Failed to cast NoiseNode to NoiseScalar: " +
                    $"NoiseNode of type {Type} has {OutputChannelCount} output channels, " +
                    "but only nodes with 1 output channel can be cast to a NoiseScalar." +
                    "Use NoiseNode.Channel(int) to access a specific channel of a NoiseNode with multiple output channels.");
            }
            return Channel(0);
        }
    }
    // AI: Add similar properties to AsScalar for Vector2 and Vector3. You can probably lift the error format into a shared constant. Document in the summary tag that they throw if they don't output the expected count.

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


    // AI: add summary tags to these. No need to document the error case, it is obvious.
    public static NoiseNode Constant(float x) => new(NoiseNodeType.Constant1__NoIn__x,
        ValidateIsRealNumber(_constantNotRealErrorMessage, "x", x));
    public static NoiseNode Constant(float x, float y) => new(NoiseNodeType.Constant2__NoIn__x_y,
        ValidateIsRealNumber(_constantNotRealErrorMessage, "x", x),
        ValidateIsRealNumber(_constantNotRealErrorMessage, "y", y));
    public static NoiseNode Constant(float x, float y, float z) => new(NoiseNodeType.Constant3__NoIn__x_y_z,
        ValidateIsRealNumber(_constantNotRealErrorMessage, "x", x),
        ValidateIsRealNumber(_constantNotRealErrorMessage, "y", y),
        ValidateIsRealNumber(_constantNotRealErrorMessage, "z", z));

    static NoiseNode InlineConstantCommunative(NoiseNode node)
    {
        // AI: assume this function is a communative operator type with 2 inputs. it is dependent on InlineConstant() this function should:
        // if both inputs are constant, return InlineConstant for the node.
        // if one input is a constant, and the other input matches the root operator type, and that input has a constant input, this should
        // return the non-constant input of the non-constant input operated with the constant inlined combination of the other input and the constant.
        // if both inputs match the root operator and both have constant inputs, this should return the operation of the two non-constant inputs operated with the inlined constant.
        // do not modify any other cases. if these cases don't apply, return the original node.
    }

    static NoiseNode InlineConstant(NoiseNode node)
    {
        return node; // leaving unimplemented until I figure out what eval api looks like.
    }

    public static NoiseScalar Add(NoiseScalar a, NoiseScalar b)
    {
        NoiseNode result = new(NoiseNodeType.Add__a_b__sum, a, b);
        result = InlineConstantCommunative(result);
        return result.AsScalar;
    }
    // AI: Add Add() functions that take in NoiseVector2 and NoiseVector3 and return same types. They should just call into scalar add
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

// AI: Create NoiseVector2, NoiseVector3 types, which contain 2 and 3 NoiseScalars respectively.

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
    // hey AI please these with a private static readonly array based cache that parses the enum names once
    public static int GetInputCount(this NoiseNodeType type) => type.ToString().Split("__")[1].Count('_') + 1;
    public static int GetOutputCount(this NoiseNodeType type) => type.ToString().Split("__")[2].Count('_') + 1;
}
