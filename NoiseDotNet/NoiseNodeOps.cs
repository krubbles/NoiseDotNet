namespace NoiseDotNet
{
    public sealed partial class NoiseNode
    {
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

        /// <summary>
        /// Clamps a scalar to [0, 1], then applies the smoothstep curve x²(3 - 2x).
        /// </summary>
        public static NoiseScalar SmoothStep(NoiseScalar value) =>
            new NoiseNode(NoiseNodeType.SmoothStep01__value__result, value).AsScalar;

        /// <summary>
        /// Applies <see cref="SmoothStep(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 SmoothStep(NoiseVector2 value) => new(
            SmoothStep(value.X),
            SmoothStep(value.Y));

        /// <summary>
        /// Applies <see cref="SmoothStep(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 SmoothStep(NoiseVector3 value) => new(
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
        public static NoiseScalar Floor(NoiseScalar value) =>
            new NoiseNode(NoiseNodeType.Floor__value__result, value).AsScalar;

        /// <summary>
        /// Applies <see cref="Floor(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 Floor(NoiseVector2 value) => new(
            Floor(value.X),
            Floor(value.Y));

        /// <summary>
        /// Applies <see cref="Floor(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 Floor(NoiseVector3 value) => new(
            Floor(value.X),
            Floor(value.Y),
            Floor(value.Z));

        /// <summary>
        /// Returns the absolute value of a scalar.
        /// </summary>
        public static NoiseScalar Abs(NoiseScalar value) => Max(value, -value);

        /// <summary>
        /// Applies <see cref="Abs(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 Abs(NoiseVector2 value) => new(
            Abs(value.X),
            Abs(value.Y));

        /// <summary>
        /// Applies <see cref="Abs(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 Abs(NoiseVector3 value) => new(
            Abs(value.X),
            Abs(value.Y),
            Abs(value.Z));

        /// <summary>
        /// Clamps a scalar between a minimum and maximum value.
        /// </summary>
        public static NoiseScalar Clamp(NoiseScalar value, NoiseScalar min, NoiseScalar max) =>
            Min(Max(value, min), max);

        /// <summary>
        /// Clamps each component between the corresponding minimum and maximum components.
        /// </summary>
        public static NoiseVector2 Clamp(NoiseVector2 value, NoiseVector2 min, NoiseVector2 max) => new(
            Clamp(value.X, min.X, max.X),
            Clamp(value.Y, min.Y, max.Y));

        /// <summary>
        /// Clamps each component between the corresponding minimum and maximum components.
        /// </summary>
        public static NoiseVector3 Clamp(NoiseVector3 value, NoiseVector3 min, NoiseVector3 max) => new(
            Clamp(value.X, min.X, max.X),
            Clamp(value.Y, min.Y, max.Y),
            Clamp(value.Z, min.Z, max.Z));

        /// <summary>
        /// Clamps every vector component between the same scalar minimum and maximum.
        /// </summary>
        public static NoiseVector2 Clamp(NoiseVector2 value, NoiseScalar min, NoiseScalar max) => new(
            Clamp(value.X, min, max),
            Clamp(value.Y, min, max));

        /// <summary>
        /// Clamps every vector component between the same scalar minimum and maximum.
        /// </summary>
        public static NoiseVector3 Clamp(NoiseVector3 value, NoiseScalar min, NoiseScalar max) => new(
            Clamp(value.X, min, max),
            Clamp(value.Y, min, max),
            Clamp(value.Z, min, max));

        /// <summary>
        /// Clamps a scalar to [0, 1].
        /// </summary>
        public static NoiseScalar Saturate(NoiseScalar value) =>
            Clamp(value, Zero, One);

        /// <summary>
        /// Applies <see cref="Saturate(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 Saturate(NoiseVector2 value) => new(
            Saturate(value.X),
            Saturate(value.Y));

        /// <summary>
        /// Applies <see cref="Saturate(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 Saturate(NoiseVector3 value) => new(
            Saturate(value.X),
            Saturate(value.Y),
            Saturate(value.Z));

        /// <summary>
        /// Returns the fractional part of a scalar in the range [0, 1).
        /// </summary>
        public static NoiseScalar Fract(NoiseScalar value) => value - Floor(value);

        /// <summary>
        /// Applies <see cref="Fract(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 Fract(NoiseVector2 value) => new(
            Fract(value.X),
            Fract(value.Y));

        /// <summary>
        /// Applies <see cref="Fract(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 Fract(NoiseVector3 value) => new(
            Fract(value.X),
            Fract(value.Y),
            Fract(value.Z));

        /// <summary>
        /// Returns value - modulus * floor(value / modulus).
        /// </summary>
        public static NoiseScalar Mod(NoiseScalar value, NoiseScalar modulus) =>
            value - modulus * Floor(value / modulus);

        /// <summary>
        /// Applies <see cref="Mod(NoiseScalar, NoiseScalar)"/> component-wise.
        /// </summary>
        public static NoiseVector2 Mod(NoiseVector2 value, NoiseVector2 modulus) => new(
            Mod(value.X, modulus.X),
            Mod(value.Y, modulus.Y));

        /// <summary>
        /// Applies <see cref="Mod(NoiseScalar, NoiseScalar)"/> component-wise.
        /// </summary>
        public static NoiseVector3 Mod(NoiseVector3 value, NoiseVector3 modulus) => new(
            Mod(value.X, modulus.X),
            Mod(value.Y, modulus.Y),
            Mod(value.Z, modulus.Z));

        /// <summary>
        /// Applies the same scalar modulus to every vector component.
        /// </summary>
        public static NoiseVector2 Mod(NoiseVector2 value, NoiseScalar modulus) => new(
            Mod(value.X, modulus),
            Mod(value.Y, modulus));

        /// <summary>
        /// Applies the same scalar modulus to every vector component.
        /// </summary>
        public static NoiseVector3 Mod(NoiseVector3 value, NoiseScalar modulus) => new(
            Mod(value.X, modulus),
            Mod(value.Y, modulus),
            Mod(value.Z, modulus));

        /// <summary>
        /// Returns e raised to a scalar power.
        /// </summary>
        public static NoiseScalar Exp(NoiseScalar value) => Pow(Constant(MathF.E), value);

        /// <summary>
        /// Applies <see cref="Exp(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector2 Exp(NoiseVector2 value) => new(
            Exp(value.X),
            Exp(value.Y));

        /// <summary>
        /// Applies <see cref="Exp(NoiseScalar)"/> to each component.
        /// </summary>
        public static NoiseVector3 Exp(NoiseVector3 value) => new(
            Exp(value.X),
            Exp(value.Y),
            Exp(value.Z));

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

    public readonly partial struct NoiseScalar
    {
        public static implicit operator NoiseScalar((NoiseNode node, int channelIndex) pair) => new(pair.node, pair.channelIndex);
        public static NoiseScalar operator +(NoiseScalar a, NoiseScalar b) => NoiseNode.Add(a, b);
        public static NoiseScalar operator -(NoiseScalar value) => NoiseNode.Negate(value);
        public static NoiseScalar operator -(NoiseScalar a, NoiseScalar b) => NoiseNode.Subtract(a, b);
        public static NoiseScalar operator *(NoiseScalar a, NoiseScalar b) => NoiseNode.Multiply(a, b);
        public static NoiseScalar operator /(NoiseScalar a, NoiseScalar b) => NoiseNode.Divide(a, b);
    }

    public readonly partial struct NoiseVector2
    {
        public static NoiseVector2 operator +(NoiseVector2 a, NoiseVector2 b) => NoiseNode.Add(a, b);
        public static NoiseVector2 operator -(NoiseVector2 value) => NoiseNode.Negate(value);
        public static NoiseVector2 operator -(NoiseVector2 a, NoiseVector2 b) => NoiseNode.Subtract(a, b);
        public static NoiseVector2 operator *(NoiseVector2 a, NoiseVector2 b) => NoiseNode.Multiply(a, b);
        public static NoiseVector2 operator *(NoiseVector2 vector, NoiseScalar scalar) => NoiseNode.Multiply(vector, scalar);
        public static NoiseVector2 operator *(NoiseScalar scalar, NoiseVector2 vector) => NoiseNode.Multiply(vector, scalar);
        public static NoiseVector2 operator /(NoiseVector2 a, NoiseVector2 b) => NoiseNode.Divide(a, b);
        public static NoiseVector2 operator /(NoiseVector2 vector, NoiseScalar scalar) => NoiseNode.Divide(vector, scalar);
    }

    public readonly partial struct NoiseVector3
    {
        public static NoiseVector3 operator +(NoiseVector3 a, NoiseVector3 b) => NoiseNode.Add(a, b);
        public static NoiseVector3 operator -(NoiseVector3 value) => NoiseNode.Negate(value);
        public static NoiseVector3 operator -(NoiseVector3 a, NoiseVector3 b) => NoiseNode.Subtract(a, b);
        public static NoiseVector3 operator *(NoiseVector3 a, NoiseVector3 b) => NoiseNode.Multiply(a, b);
        public static NoiseVector3 operator *(NoiseVector3 vector, NoiseScalar scalar) => NoiseNode.Multiply(vector, scalar);
        public static NoiseVector3 operator *(NoiseScalar scalar, NoiseVector3 vector) => NoiseNode.Multiply(vector, scalar);
        public static NoiseVector3 operator /(NoiseVector3 a, NoiseVector3 b) => NoiseNode.Divide(a, b);
        public static NoiseVector3 operator /(NoiseVector3 vector, NoiseScalar scalar) => NoiseNode.Divide(vector, scalar);
    }
}
