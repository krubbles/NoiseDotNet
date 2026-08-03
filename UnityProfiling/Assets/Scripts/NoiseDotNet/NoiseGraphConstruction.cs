using System;
using System.Collections.Generic;

namespace NoiseDotNet
{
    /// <summary>
    /// API for constructing and evaluating noise graphs.
    /// </summary>
    public static partial class NoiseGraph
    {
        // This file includes all the public functions
        // related to constructing noise graphs.

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

        /// <summary>
        /// Constant scalar zero.
        /// </summary>
        public static readonly NoiseScalar Zero = Constant(0f);

        /// <summary>
        /// Constant scalar one.
        /// </summary>
        public static readonly NoiseScalar One = Constant(1f);

        /// <summary>
        /// One-dimensional coordinate input.
        /// </summary>
        public static readonly NoiseScalar X =
            new NoiseNode(NoiseNodeType.Coords1__NoIn__x, Array.Empty<NoiseScalar>()).AsScalar;

        /// <summary>
        /// Two-dimensional coordinate inputs.
        /// </summary>
        public static readonly NoiseVector2 XY =
            new NoiseNode(NoiseNodeType.Coords2__NoIn__x_y, Array.Empty<NoiseScalar>()).AsVector2;

        /// <summary>
        /// Three-dimensional coordinate inputs.
        /// </summary>
        public static readonly NoiseVector3 XYZ =
            new NoiseNode(NoiseNodeType.Coords3__NoIn__x_y_z, Array.Empty<NoiseScalar>()).AsVector3;

        /// <summary>
        /// Returns the one-dimensional coordinate input multiplied by a frequency scale.
        /// </summary>
        public static NoiseScalar Coordinates(NoiseScalar frequencyScale) => X * frequencyScale;

        /// <summary>
        /// Returns the two-dimensional coordinate inputs multiplied component-wise by frequency scales.
        /// </summary>
        public static NoiseVector2 Coordinates(NoiseVector2 frequencyScales) => XY * frequencyScales;

        /// <summary>
        /// Returns the three-dimensional coordinate inputs multiplied component-wise by frequency scales.
        /// </summary>
        public static NoiseVector3 Coordinates(NoiseVector3 frequencyScales) => XYZ * frequencyScales;

        /// <summary>
        /// Creates a two-dimensional Perlin noise function.
        /// </summary>
        public static NoiseScalar Perlin(NoiseVector2 coordinates) =>
            new NoiseNode(
                NoiseNodeType.Perlin2D_noise__x_y__noise,
                coordinates.X,
                coordinates.Y).AsScalar;

        /// <summary>
        /// Creates a three-dimensional Perlin noise function.
        /// </summary>
        public static NoiseScalar Perlin(NoiseVector3 coordinates) =>
            new NoiseNode(
                NoiseNodeType.Perlin3D_noise__x_y_z__noise,
                coordinates.X,
                coordinates.Y,
                coordinates.Z).AsScalar;

        /// <summary>
        /// Creates a two-dimensional cellular noise function.
        /// Center dist is the distance to the nearest cell center,
        /// Edge distance is the distance to the nearest cell edge.
        /// </summary>
        public static (NoiseScalar centerDist, NoiseScalar edgeDist) Cellular(NoiseVector2 coordinates)
        {
            NoiseNode node = new(
                NoiseNodeType.Cellular2_noise__x_y__center_edge,
                coordinates.X,
                coordinates.Y);
            return (node.Channel(0), node.Channel(1));
        }

        /// <summary>
        /// Creates a three-dimensional cellular noise function.
        /// Center dist is the distance to the nearest cell center,
        /// Edge distance is the distance to the nearest cell edge.
        /// </summary>
        public static (NoiseScalar centerDist, NoiseScalar edgeDist) Cellular(NoiseVector3 coordinates)
        {
            NoiseNode node = new(
                NoiseNodeType.Cellular3_noise__x_y_z__center_edge,
                coordinates.X,
                coordinates.Y,
                coordinates.Z);
            return (node.Channel(0), node.Channel(1));
        }

        /// <summary>
        /// Returns a clone of <paramref name="original"/> with every two-dimensional noise
        /// function's coordinates transformed by the basis vectors <paramref name="i"/> and
        /// <paramref name="j"/>.
        /// </summary>
        public static NoiseScalar Transform(this NoiseScalar original, NoiseVector2 i, NoiseVector2 j) =>
            Transform(
                new NoiseScalar[] { original },
                new NoiseScalar[][]
                {
                    new NoiseScalar[] { i.X, i.Y },
                    new NoiseScalar[] { j.X, j.Y },
                })[0];

        /// <inheritdoc cref="Transform(NoiseScalar, NoiseVector2, NoiseVector2)"/>
        public static NoiseVector2 Transform(this NoiseVector2 original, NoiseVector2 i, NoiseVector2 j)
        {
            NoiseScalar[] transformed = Transform(
                new NoiseScalar[] { original.X, original.Y },
                new NoiseScalar[][]
                {
                    new NoiseScalar[] { i.X, i.Y },
                    new NoiseScalar[] { j.X, j.Y },
                });
            return new(transformed[0], transformed[1]);
        }

        /// <inheritdoc cref="Transform(NoiseScalar, NoiseVector2, NoiseVector2)"/>
        public static NoiseVector3 Transform(this NoiseVector3 original, NoiseVector2 i, NoiseVector2 j)
        {
            NoiseScalar[] transformed = Transform(
                new NoiseScalar[] { original.X, original.Y, original.Z },
                new NoiseScalar[][]
                {
                    new NoiseScalar[] { i.X, i.Y },
                    new NoiseScalar[] { j.X, j.Y },
                });
            return new(transformed[0], transformed[1], transformed[2]);
        }

        /// <summary>
        /// Returns a clone of <paramref name="original"/> with every three-dimensional noise
        /// function's coordinates transformed by the basis vectors <paramref name="i"/>,
        /// <paramref name="j"/>, and <paramref name="k"/>.
        /// </summary>
        public static NoiseScalar Transform(this NoiseScalar original, NoiseVector3 i, NoiseVector3 j, NoiseVector3 k) =>
            Transform(
                new NoiseScalar[] { original },
                new NoiseScalar[][]
                {
                    new NoiseScalar[] { i.X, i.Y, i.Z },
                    new NoiseScalar[] { j.X, j.Y, j.Z },
                    new NoiseScalar[] { k.X, k.Y, k.Z },
                })[0];

        /// <inheritdoc cref="Transform(NoiseScalar, NoiseVector3, NoiseVector3, NoiseVector3)"/>
        public static NoiseVector2 Transform(this NoiseVector2 original, NoiseVector3 i, NoiseVector3 j, NoiseVector3 k)
        {
            NoiseScalar[] transformed = Transform(
                new NoiseScalar[] { original.X, original.Y },
                new NoiseScalar[][]
                {
                    new NoiseScalar[] { i.X, i.Y, i.Z },
                    new NoiseScalar[] { j.X, j.Y, j.Z },
                    new NoiseScalar[] { k.X, k.Y, k.Z },
                });
            return new(transformed[0], transformed[1]);
        }

        /// <inheritdoc cref="Transform(NoiseScalar, NoiseVector3, NoiseVector3, NoiseVector3)"/>
        public static NoiseVector3 Transform(this NoiseVector3 original,NoiseVector3 i, NoiseVector3 j, NoiseVector3 k)
        {
            NoiseScalar[] transformed = Transform(
                new NoiseScalar[] { original.X, original.Y, original.Z },
                new NoiseScalar[][]
                {
                    new NoiseScalar[] { i.X, i.Y, i.Z },
                    new NoiseScalar[] { j.X, j.Y, j.Z },
                    new NoiseScalar[] { k.X, k.Y, k.Z },
                });
            return new(transformed[0], transformed[1], transformed[2]);
        }

        /// <summary>
        /// Stretches every two-dimensional noise function in <paramref name="original"/> along
        /// its x and y axes.
        /// </summary>
        public static NoiseScalar Stretch(this NoiseScalar original, NoiseScalar x, NoiseScalar y) =>
            Transform(original, new(x, Zero), new(Zero, y));

        /// <inheritdoc cref="Stretch(NoiseScalar, NoiseScalar, NoiseScalar)"/>
        public static NoiseVector2 Stretch(this NoiseVector2 original, NoiseScalar x, NoiseScalar y) =>
            Transform(original, new(x, Zero), new(Zero, y));

        /// <inheritdoc cref="Stretch(NoiseScalar, NoiseScalar, NoiseScalar)"/>
        public static NoiseVector3 Stretch(this NoiseVector3 original, NoiseScalar x, NoiseScalar y) =>
            Transform(original, new(x, Zero), new(Zero, y));

        /// <summary>
        /// Stretches every two-dimensional noise function in <paramref name="original"/> by the
        /// corresponding component of <paramref name="scale"/>.
        /// </summary>
        public static NoiseScalar Stretch(this NoiseScalar original, NoiseVector2 scale) =>
            Stretch(original, scale.X, scale.Y);

        /// <inheritdoc cref="Stretch(NoiseScalar, NoiseVector2)"/>
        public static NoiseVector2 Stretch(this NoiseVector2 original, NoiseVector2 scale) =>
            Stretch(original, scale.X, scale.Y);

        /// <inheritdoc cref="Stretch(NoiseScalar, NoiseVector2)"/>
        public static NoiseVector3 Stretch(this NoiseVector3 original, NoiseVector2 scale) =>
            Stretch(original, scale.X, scale.Y);

        /// <summary>
        /// Stretches every three-dimensional noise function in <paramref name="original"/> along
        /// its x, y, and z axes.
        /// </summary>
        public static NoiseScalar Stretch(this NoiseScalar original, NoiseScalar x, NoiseScalar y, NoiseScalar z) =>
            Transform(original, new(x, Zero, Zero), new(Zero, y, Zero), new(Zero, Zero, z));

        /// <inheritdoc cref="Stretch(NoiseScalar, NoiseScalar, NoiseScalar, NoiseScalar)"/>
        public static NoiseVector2 Stretch(this NoiseVector2 original, NoiseScalar x, NoiseScalar y, NoiseScalar z) =>
            Transform(original, new(x, Zero, Zero), new(Zero, y, Zero), new(Zero, Zero, z));

        /// <inheritdoc cref="Stretch(NoiseScalar, NoiseScalar, NoiseScalar, NoiseScalar)"/>
        public static NoiseVector3 Stretch(this NoiseVector3 original, NoiseScalar x, NoiseScalar y, NoiseScalar z) =>
            Transform(original, new(x, Zero, Zero), new(Zero, y, Zero), new(Zero, Zero, z));

        /// <summary>
        /// Stretches every three-dimensional noise function in <paramref name="original"/> by the
        /// corresponding component of <paramref name="scale"/>.
        /// </summary>
        public static NoiseScalar Stretch(this NoiseScalar original, NoiseVector3 scale) =>
            Stretch(original, scale.X, scale.Y, scale.Z);

        /// <inheritdoc cref="Stretch(NoiseScalar, NoiseVector3)"/>
        public static NoiseVector2 Stretch(this NoiseVector2 original, NoiseVector3 scale) =>
            Stretch(original, scale.X, scale.Y, scale.Z);

        /// <inheritdoc cref="Stretch(NoiseScalar, NoiseVector3)"/>
        public static NoiseVector3 Stretch(this NoiseVector3 original, NoiseVector3 scale) =>
            Stretch(original, scale.X, scale.Y, scale.Z);

        /// <summary>
        /// Combines frequency-scaled, amplitude-weighted copies of a two-dimensional noise
        /// expression into a single fractal Brownian motion NoiseScalar.
        /// <para>
        /// The first octave is <paramref name="value"/> unchanged. Each subsequent octave stretches
        /// <paramref name="value"/>'s coordinate inputs by <paramref name="lacunarity"/> raised to the
        /// octave index, and scales its contribution by <paramref name="persistence"/> raised to the
        /// octave index, before adding it to the sum.
        /// </para>
        /// </summary>
        public static NoiseScalar Fractal(this NoiseScalar value, NoiseScalar persistence, NoiseScalar lacunarity, int octaves)
        {
            if (octaves < 1)
                throw new ArgumentOutOfRangeException(nameof(octaves), octaves, "Octave count must be at least 1.");

            NoiseScalar sum = value;
            NoiseScalar frequency = lacunarity;
            NoiseScalar amplitude = persistence;
            for (int octave = 1; octave < octaves; octave++)
            {
                sum += value.Stretch(frequency, frequency) * amplitude;
                frequency *= lacunarity;
                amplitude *= persistence;
            }
            return sum;
        }

        static NoiseScalar[] Transform(NoiseScalar[] originals, NoiseScalar[][] basis)
        {
            Dictionary<NoiseNode, NoiseNode> clones = new();
            NoiseScalar[] transformed = new NoiseScalar[originals.Length];
            for (int outputIndex = 0; outputIndex < originals.Length; outputIndex++)
                transformed[outputIndex] = Clone(originals[outputIndex]);
            return transformed;

            NoiseScalar Clone(NoiseScalar original)
            {
                NoiseNode originalNode = original.Node;
                if (!clones.TryGetValue(originalNode, out NoiseNode? clone))
                {
                    clone = CloneNode(originalNode);
                    clones.Add(originalNode, clone);
                }
                return clone.Channel(original.ChannelIndex);
            }

            NoiseNode CloneNode(NoiseNode original)
            {
                if (original.Type.IsNoise())
                    return TransformNoise(original);

                if (original.IsConstant)
                    return new NoiseNode(original.Type, original.ConstantValues.ToArray());

                NoiseScalar[] clonedInputs = new NoiseScalar[original.InputChannelCount];
                for (int inputIndex = 0; inputIndex < clonedInputs.Length; inputIndex++)
                    clonedInputs[inputIndex] = Clone(original.Inputs[inputIndex]);
                return new NoiseNode(original.Type, clonedInputs);
            }

            NoiseNode TransformNoise(NoiseNode original)
            {
                if (original.InputChannelCount != basis.Length)
                {
                    throw new InvalidOperationException(
                        $"Cannot apply a {basis.Length}D transform to noise node type {original.Type}, " +
                        $"which has {original.InputChannelCount} coordinate inputs.");
                }

                NoiseScalar[] remappedCoordinates = new NoiseScalar[basis.Length];
                for (int outputAxis = 0; outputAxis < remappedCoordinates.Length; outputAxis++)
                {
                    NoiseScalar remapped = original.Inputs[0] * basis[0][outputAxis];
                    for (int inputAxis = 1; inputAxis < basis.Length; inputAxis++)
                        remapped += original.Inputs[inputAxis] * basis[inputAxis][outputAxis];
                    remappedCoordinates[outputAxis] = remapped;
                }
                return new NoiseNode(original.Type, remappedCoordinates);
            }
        }


        static NoiseNode InlineConstantCommunative(NoiseNode node)
        {
            NoiseScalar left = node.Inputs[0];
            NoiseScalar right = node.Inputs[1];

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
                if (operandNode.Type != operatorType || operandNode.Inputs.Length != 2)
                    return false;

                NoiseScalar left = operandNode.Inputs[0];
                NoiseScalar right = operandNode.Inputs[1];
                if (left.IsConstant == right.IsConstant)
                    return false;

                value = left.IsConstant ? right : left;
                constant = left.IsConstant ? left : right;
                return true;
            }
        }

        static NoiseNode InlineConstant(NoiseNode node)
        {
            foreach (NoiseScalar input in node.Inputs)
                if (!input.IsConstant)
                    return node;

            return node;
        }

        static bool IsConstant(NoiseScalar value, float expected) =>
            value.IsConstant &&
            value.Node.ConstantValues[value.ChannelIndex] == expected;

        public static NoiseScalar Add(this NoiseScalar a, NoiseScalar b)
        {
            if (IsConstant(a, 0f))
                return b;
            if (IsConstant(b, 0f))
                return a;

            NoiseNode result = new(NoiseNodeType.Add__a_b__sum, a, b);
            result = InlineConstantCommunative(result);
            return result.AsScalar;
        }

        public static NoiseVector2 Add(this NoiseVector2 a, NoiseVector2 b) => new(
            Add(a.X, b.X),
            Add(a.Y, b.Y));

        public static NoiseVector3 Add(this NoiseVector3 a, NoiseVector3 b) => new(
            Add(a.X, b.X),
            Add(a.Y, b.Y),
            Add(a.Z, b.Z));

        /// <summary>
        /// Returns the smaller of two scalar values.
        /// </summary>
        public static NoiseScalar Min(this NoiseScalar a, NoiseScalar b)
        {
            NoiseNode result = new(NoiseNodeType.Min__a_b__min, a, b);
            result = InlineConstantCommunative(result);
            return result.AsScalar;
        }

        /// <summary>
        /// Returns the component-wise minimum of two vectors.
        /// </summary>
        public static NoiseVector2 Min(this NoiseVector2 a, NoiseVector2 b) => new(
            Min(a.X, b.X),
            Min(a.Y, b.Y));

        /// <summary>
        /// Returns the component-wise minimum of two vectors.
        /// </summary>
        public static NoiseVector3 Min(this NoiseVector3 a, NoiseVector3 b) => new(
            Min(a.X, b.X),
            Min(a.Y, b.Y),
            Min(a.Z, b.Z));

        /// <summary>
        /// Returns the larger of two scalar values.
        /// </summary>
        public static NoiseScalar Max(this NoiseScalar a, NoiseScalar b)
        {
            NoiseNode result = new(NoiseNodeType.Max__a_b__max, a, b);
            result = InlineConstantCommunative(result);
            return result.AsScalar;
        }

        /// <summary>
        /// Returns the component-wise maximum of two vectors.
        /// </summary>
        public static NoiseVector2 Max(this NoiseVector2 a, NoiseVector2 b) => new(
            Max(a.X, b.X),
            Max(a.Y, b.Y));

        /// <summary>
        /// Returns the component-wise maximum of two vectors.
        /// </summary>
        public static NoiseVector3 Max(this NoiseVector3 a, NoiseVector3 b) => new(
            Max(a.X, b.X),
            Max(a.Y, b.Y),
            Max(a.Z, b.Z));

        /// <summary>
        /// Raises a scalar value to a scalar power.
        /// </summary>
        public static NoiseScalar Pow(this NoiseScalar value, NoiseScalar power) =>
            new NoiseNode(NoiseNodeType.Pow__value_power__result, value, power).AsScalar;

        /// <summary>
        /// Raises each component of a vector to the corresponding component of another vector.
        /// </summary>
        public static NoiseVector2 Pow(this NoiseVector2 value, NoiseVector2 power) => new(
            Pow(value.X, power.X),
            Pow(value.Y, power.Y));

        /// <summary>
        /// Raises each component of a vector to the corresponding component of another vector.
        /// </summary>
        public static NoiseVector3 Pow(this NoiseVector3 value, NoiseVector3 power) => new(
            Pow(value.X, power.X),
            Pow(value.Y, power.Y),
            Pow(value.Z, power.Z));

        /// <summary>
        /// Raises each component of a vector to the same scalar power.
        /// </summary>
        public static NoiseVector2 Pow(this NoiseVector2 value, NoiseScalar power) => new(
            Pow(value.X, power),
            Pow(value.Y, power));

        /// <summary>
        /// Raises each component of a vector to the same scalar power.
        /// </summary>
        public static NoiseVector3 Pow(this NoiseVector3 value, NoiseScalar power) => new(
            Pow(value.X, power),
            Pow(value.Y, power),
            Pow(value.Z, power));

        /// <summary>
        /// Clamps a scalar to [0, 1], then applies the smoothstep curve x²(3 - 2x).
        /// </summary>
        public static NoiseScalar SmoothStep(this NoiseScalar value) =>
            new NoiseNode(NoiseNodeType.SmoothStep01__value__result, value).AsScalar;

        /// <summary>
        /// Applies <see cref="SmoothStep(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 SmoothStep(this NoiseVector2 value) => new(
            SmoothStep(value.X),
            SmoothStep(value.Y));

        /// <summary>
        /// Applies <see cref="SmoothStep(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 SmoothStep(this NoiseVector3 value) => new(
            SmoothStep(value.X),
            SmoothStep(value.Y),
            SmoothStep(value.Z));

        /// <summary>
        /// Linearly interpolates from <paramref name="a"/> to <paramref name="b"/> by
        /// <paramref name="t"/>. The interpolation factor is not clamped.
        /// </summary>
        public static NoiseScalar Lerp(NoiseScalar a, NoiseScalar b, NoiseScalar t) =>
            new NoiseNode(NoiseNodeType.Lerp__a_b_t__result, a, b, t).AsScalar;

        /// <summary>
        /// Component-wise linear interpolation using one interpolation factor.
        /// </summary>
        public static NoiseVector2 Lerp(NoiseVector2 a, NoiseVector2 b, NoiseScalar t) => new(
            Lerp(a.X, b.X, t),
            Lerp(a.Y, b.Y, t));

        /// <summary>
        /// Component-wise linear interpolation using one interpolation factor.
        /// </summary>
        public static NoiseVector3 Lerp(NoiseVector3 a, NoiseVector3 b, NoiseScalar t) => new(
            Lerp(a.X, b.X, t),
            Lerp(a.Y, b.Y, t),
            Lerp(a.Z, b.Z, t));

        /// <summary>
        /// Component-wise linear interpolation using a separate factor for each component.
        /// </summary>
        public static NoiseVector2 Lerp(NoiseVector2 a, NoiseVector2 b, NoiseVector2 t) => new(
            Lerp(a.X, b.X, t.X),
            Lerp(a.Y, b.Y, t.Y));

        /// <summary>
        /// Component-wise linear interpolation using a separate factor for each component.
        /// </summary>
        public static NoiseVector3 Lerp(NoiseVector3 a, NoiseVector3 b, NoiseVector3 t) => new(
            Lerp(a.X, b.X, t.X),
            Lerp(a.Y, b.Y, t.Y),
            Lerp(a.Z, b.Z, t.Z));

        /// <summary>
        /// Returns the largest integer less than or equal to a scalar value.
        /// </summary>
        public static NoiseScalar Floor(this NoiseScalar value) =>
            new NoiseNode(NoiseNodeType.Floor__value__result, value).AsScalar;

        /// <summary>
        /// Applies <see cref="Floor(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 Floor(this NoiseVector2 value) => new(
            Floor(value.X),
            Floor(value.Y));

        /// <summary>
        /// Applies <see cref="Floor(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 Floor(this NoiseVector3 value) => new(
            Floor(value.X),
            Floor(value.Y),
            Floor(value.Z));

        /// <summary>
        /// Returns the absolute value of a scalar.
        /// </summary>
        public static NoiseScalar Abs(this NoiseScalar value) => Max(value, -value);

        /// <summary>
        /// Applies <see cref="Abs(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 Abs(this NoiseVector2 value) => new(
            Abs(value.X),
            Abs(value.Y));

        /// <summary>
        /// Applies <see cref="Abs(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 Abs(this NoiseVector3 value) => new(
            Abs(value.X),
            Abs(value.Y),
            Abs(value.Z));

        /// <summary>
        /// Clamps a scalar between a minimum and maximum value.
        /// </summary>
        public static NoiseScalar Clamp(this NoiseScalar value, NoiseScalar min, NoiseScalar max) =>
            Min(Max(value, min), max);

        /// <summary>
        /// Clamps each component between the corresponding minimum and maximum components.
        /// </summary>
        public static NoiseVector2 Clamp(this NoiseVector2 value, NoiseVector2 min, NoiseVector2 max) => new(
            Clamp(value.X, min.X, max.X),
            Clamp(value.Y, min.Y, max.Y));

        /// <summary>
        /// Clamps each component between the corresponding minimum and maximum components.
        /// </summary>
        public static NoiseVector3 Clamp(this NoiseVector3 value, NoiseVector3 min, NoiseVector3 max) => new(
            Clamp(value.X, min.X, max.X),
            Clamp(value.Y, min.Y, max.Y),
            Clamp(value.Z, min.Z, max.Z));

        /// <summary>
        /// Clamps every vector component between the same scalar minimum and maximum.
        /// </summary>
        public static NoiseVector2 Clamp(this NoiseVector2 value, NoiseScalar min, NoiseScalar max) => new(
            Clamp(value.X, min, max),
            Clamp(value.Y, min, max));

        /// <summary>
        /// Clamps every vector component between the same scalar minimum and maximum.
        /// </summary>
        public static NoiseVector3 Clamp(this NoiseVector3 value, NoiseScalar min, NoiseScalar max) => new(
            Clamp(value.X, min, max),
            Clamp(value.Y, min, max),
            Clamp(value.Z, min, max));

        /// <summary>
        /// Clamps a scalar to [0, 1].
        /// </summary>
        public static NoiseScalar Saturate(this NoiseScalar value) =>
            Clamp(value, Zero, One);

        /// <summary>
        /// Applies <see cref="Saturate(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 Saturate(this NoiseVector2 value) => new(
            Saturate(value.X),
            Saturate(value.Y));

        /// <summary>
        /// Applies <see cref="Saturate(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 Saturate(this NoiseVector3 value) => new(
            Saturate(value.X),
            Saturate(value.Y),
            Saturate(value.Z));

        /// <summary>
        /// Returns the fractional part of a scalar in the range [0, 1).
        /// </summary>
        public static NoiseScalar Fract(this NoiseScalar value) => value - Floor(value);

        /// <summary>
        /// Applies <see cref="Fract(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 Fract(this NoiseVector2 value) => new(
            Fract(value.X),
            Fract(value.Y));

        /// <summary>
        /// Applies <see cref="Fract(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 Fract(this NoiseVector3 value) => new(
            Fract(value.X),
            Fract(value.Y),
            Fract(value.Z));

        /// <summary>
        /// Returns value - modulus * floor(value / modulus).
        /// </summary>
        public static NoiseScalar Mod(this NoiseScalar value, NoiseScalar modulus) =>
            value - modulus * Floor(value / modulus);

        /// <summary>
        /// Applies <see cref="Mod(NoiseScalar, NoiseScalar)"/> component-wise.
        /// </summary>
        public static NoiseVector2 Mod(this NoiseVector2 value, NoiseVector2 modulus) => new(
            Mod(value.X, modulus.X),
            Mod(value.Y, modulus.Y));

        /// <summary>
        /// Applies <see cref="Mod(NoiseScalar, NoiseScalar)"/> component-wise.
        /// </summary>
        public static NoiseVector3 Mod(this NoiseVector3 value, NoiseVector3 modulus) => new(
            Mod(value.X, modulus.X),
            Mod(value.Y, modulus.Y),
            Mod(value.Z, modulus.Z));

        /// <summary>
        /// Applies the same scalar modulus to every vector component.
        /// </summary>
        public static NoiseVector2 Mod(this NoiseVector2 value, NoiseScalar modulus) => new(
            Mod(value.X, modulus),
            Mod(value.Y, modulus));

        /// <summary>
        /// Applies the same scalar modulus to every vector component.
        /// </summary>
        public static NoiseVector3 Mod(this NoiseVector3 value, NoiseScalar modulus) => new(
            Mod(value.X, modulus),
            Mod(value.Y, modulus),
            Mod(value.Z, modulus));

        /// <summary>
        /// Returns e raised to a scalar power.
        /// </summary>
        public static NoiseScalar Exp(this NoiseScalar value) => Pow(Constant(MathF.E), value);

        /// <summary>
        /// Applies <see cref="Exp(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 Exp(this NoiseVector2 value) => new(
            Exp(value.X),
            Exp(value.Y));

        /// <summary>
        /// Applies <see cref="Exp(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 Exp(this NoiseVector3 value) => new(
            Exp(value.X),
            Exp(value.Y),
            Exp(value.Z));

        public static NoiseScalar Negate(this NoiseScalar value) =>
            new NoiseNode(NoiseNodeType.Negate__value__negated, value).AsScalar;

        public static NoiseVector2 Negate(this NoiseVector2 value) => new(
            Negate(value.X),
            Negate(value.Y));

        public static NoiseVector3 Negate(this NoiseVector3 value) => new(
            Negate(value.X),
            Negate(value.Y),
            Negate(value.Z));

        public static NoiseScalar Subtract(this NoiseScalar a, NoiseScalar b) => Add(a, Negate(b));

        public static NoiseVector2 Subtract(this NoiseVector2 a, NoiseVector2 b) => Add(a, Negate(b));

        public static NoiseVector3 Subtract(this NoiseVector3 a, NoiseVector3 b) => Add(a, Negate(b));

        public static NoiseScalar Multiply(this NoiseScalar a, NoiseScalar b)
        {
            if (IsConstant(a, 0f) || IsConstant(b, 0f))
                return Zero;
            if (IsConstant(a, 1f))
                return b;
            if (IsConstant(b, 1f))
                return a;

            NoiseNode result = new(NoiseNodeType.Multiply__a_b__product, a, b);
            result = InlineConstantCommunative(result);
            return result.AsScalar;
        }

        public static NoiseVector2 Multiply(this NoiseVector2 a, NoiseVector2 b) => new(
            Multiply(a.X, b.X),
            Multiply(a.Y, b.Y));

        public static NoiseVector3 Multiply(this NoiseVector3 a, NoiseVector3 b) => new(
            Multiply(a.X, b.X),
            Multiply(a.Y, b.Y),
            Multiply(a.Z, b.Z));

        public static NoiseVector2 Multiply(this NoiseVector2 vector, NoiseScalar scalar) => new(
            Multiply(vector.X, scalar),
            Multiply(vector.Y, scalar));

        public static NoiseVector3 Multiply(this NoiseVector3 vector, NoiseScalar scalar) => new(
            Multiply(vector.X, scalar),
            Multiply(vector.Y, scalar),
            Multiply(vector.Z, scalar));

        public static NoiseScalar Inverse(this NoiseScalar value) =>
            new NoiseNode(NoiseNodeType.Inverse__value__inverse, value).AsScalar;

        public static NoiseVector2 Inverse(this NoiseVector2 value) => new(
            Inverse(value.X),
            Inverse(value.Y));

        public static NoiseVector3 Inverse(this NoiseVector3 value) => new(
            Inverse(value.X),
            Inverse(value.Y),
            Inverse(value.Z));

        public static NoiseScalar Divide(this NoiseScalar a, NoiseScalar b) => Multiply(a, Inverse(b));

        public static NoiseVector2 Divide(this NoiseVector2 a, NoiseVector2 b) => Multiply(a, Inverse(b));

        public static NoiseVector3 Divide(this NoiseVector3 a, NoiseVector3 b) => Multiply(a, Inverse(b));

        public static NoiseVector2 Divide(this NoiseVector2 vector, NoiseScalar scalar) =>
            Multiply(vector, Inverse(scalar));

        public static NoiseVector3 Divide(this NoiseVector3 vector, NoiseScalar scalar) =>
            Multiply(vector, Inverse(scalar));
    }

    public readonly partial struct NoiseScalar
    {
        public static implicit operator NoiseScalar((NoiseNode node, int channelIndex) pair) => new(pair.node, pair.channelIndex);
        public static implicit operator NoiseScalar(float value) => NoiseGraph.Constant(value);
        public static NoiseScalar operator +(NoiseScalar a, NoiseScalar b) => NoiseGraph.Add(a, b);
        public static NoiseScalar operator -(NoiseScalar value) => NoiseGraph.Negate(value);
        public static NoiseScalar operator -(NoiseScalar a, NoiseScalar b) => NoiseGraph.Subtract(a, b);
        public static NoiseScalar operator *(NoiseScalar a, NoiseScalar b) => NoiseGraph.Multiply(a, b);
        public static NoiseScalar operator /(NoiseScalar a, NoiseScalar b) => NoiseGraph.Divide(a, b);
    }

    public readonly partial struct NoiseVector2
    {
        public static implicit operator NoiseVector2((float x, float y) value) =>
            NoiseGraph.Constant(value.x, value.y);

        public static NoiseVector2 operator +(NoiseVector2 a, NoiseVector2 b) => NoiseGraph.Add(a, b);
        public static NoiseVector2 operator -(NoiseVector2 value) => NoiseGraph.Negate(value);
        public static NoiseVector2 operator -(NoiseVector2 a, NoiseVector2 b) => NoiseGraph.Subtract(a, b);
        public static NoiseVector2 operator *(NoiseVector2 a, NoiseVector2 b) => NoiseGraph.Multiply(a, b);
        public static NoiseVector2 operator *(NoiseVector2 vector, NoiseScalar scalar) => NoiseGraph.Multiply(vector, scalar);
        public static NoiseVector2 operator *(NoiseScalar scalar, NoiseVector2 vector) => NoiseGraph.Multiply(vector, scalar);
        public static NoiseVector2 operator /(NoiseVector2 a, NoiseVector2 b) => NoiseGraph.Divide(a, b);
        public static NoiseVector2 operator /(NoiseVector2 vector, NoiseScalar scalar) => NoiseGraph.Divide(vector, scalar);
    }

    public readonly partial struct NoiseVector3
    {
        public static implicit operator NoiseVector3((float x, float y, float z) value) =>
            NoiseGraph.Constant(value.x, value.y, value.z);

        public static NoiseVector3 operator +(NoiseVector3 a, NoiseVector3 b) => NoiseGraph.Add(a, b);
        public static NoiseVector3 operator -(NoiseVector3 value) => NoiseGraph.Negate(value);
        public static NoiseVector3 operator -(NoiseVector3 a, NoiseVector3 b) => NoiseGraph.Subtract(a, b);
        public static NoiseVector3 operator *(NoiseVector3 a, NoiseVector3 b) => NoiseGraph.Multiply(a, b);
        public static NoiseVector3 operator *(NoiseVector3 vector, NoiseScalar scalar) => NoiseGraph.Multiply(vector, scalar);
        public static NoiseVector3 operator *(NoiseScalar scalar, NoiseVector3 vector) => NoiseGraph.Multiply(vector, scalar);
        public static NoiseVector3 operator /(NoiseVector3 a, NoiseVector3 b) => NoiseGraph.Divide(a, b);
        public static NoiseVector3 operator /(NoiseVector3 vector, NoiseScalar scalar) => NoiseGraph.Divide(vector, scalar);
    }
}
