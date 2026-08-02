using NoiseDotNet;

namespace Tests
{
    // NoiseNodeTypeExtensions.TryGetMetadata is a hand-written switch (not a lookup table built via
    // reflection) because it must be callable from Burst compiled code. These tests make sure that
    // switch can't silently drift out of sync with the NoiseNodeType enum: every member's metadata,
    // as parsed from its name by NoiseNodeTypeExtensions.ParseMetadataFromName, must match the switch.
    public class NoiseNodeTypeMetadataTests
    {
        [Test]
        public void TryGetMetadataMatchesNameForEveryDefinedType()
        {
            foreach (NoiseNodeType type in Enum.GetValues<NoiseNodeType>())
            {
                if (type == NoiseNodeType.Null)
                    continue;

                Assert.That(
                    NoiseNodeTypeExtensions.TryGetMetadata(type, out var switchMetadata),
                    Is.True,
                    $"NoiseNodeTypeExtensions.TryGetMetadata has no case for {type}. " +
                    "Add one and keep it in sync with the type's name.");

                var expected = NoiseNodeTypeExtensions.ParseMetadataFromName(type);
                Assert.That(
                    switchMetadata,
                    Is.EqualTo(expected),
                    $"NoiseNodeTypeExtensions.TryGetMetadata case for {type} is {switchMetadata}, " +
                    $"but its name implies {expected}.");
            }
        }

        [Test]
        public void TryGetMetadataReturnsFalseForNullAndUndefinedValues()
        {
            Assert.That(NoiseNodeTypeExtensions.TryGetMetadata(NoiseNodeType.Null, out _), Is.False);
            Assert.That(NoiseNodeTypeExtensions.TryGetMetadata((NoiseNodeType)(-1), out _), Is.False);
            Assert.That(NoiseNodeTypeExtensions.TryGetMetadata((NoiseNodeType)9999, out _), Is.False);
        }
    }
}
