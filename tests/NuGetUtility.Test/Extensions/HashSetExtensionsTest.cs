// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using AutoFixture;
using NuGetUtility.Extensions;

namespace NuGetUtility.Test.Extensions
{
    public abstract class HashSetExtensionsTestBase<T>
    {
        private readonly Fixture _fixture;

        protected HashSetExtensionsTestBase()
        {
            _fixture = new Fixture();
        }

        [Test]
        public async Task AddMany_Should_AddNewElementsToHashSet()
        {
            HashSet<T> uut = new HashSet<T>(_fixture.CreateMany<T>());
            T[] newElements = _fixture.CreateMany<T>().ToArray();
            ImmutableList<T> initialElements = [.. uut];

            uut.AddRange(newElements);

            await Assert.That(uut).IsEquivalentTo(initialElements.AddRange(newElements).Distinct());
        }

        [Test]
        public async Task AddMany_Should_OnlyAddNewItems()
        {
            HashSet<T> uut = new HashSet<T>(_fixture.CreateMany<T>());
            T[] newElements = _fixture.CreateMany<T>().ToArray();
            ImmutableList<T> initialElements = [.. uut];

            uut.AddRange(initialElements.AddRange(newElements));

            await Assert.That(uut).IsEquivalentTo(initialElements.AddRange(newElements).Distinct());
        }

        [Test]
        public async Task AddMany_Should_KeepSameHashSetIfOnlyAddingSameElements()
        {
            HashSet<T> uut = new HashSet<T>(_fixture.CreateMany<T>());
            ImmutableList<T> initialElements = [.. uut];

            uut.AddRange(initialElements);
            uut.AddRange(initialElements);
            uut.AddRange(initialElements);

            await Assert.That(uut).IsEquivalentTo(initialElements);
        }
    }

    [InheritsTests]
    public sealed class HashSetExtensionsStringTest : HashSetExtensionsTestBase<string>;

    [InheritsTests]
    public sealed class HashSetExtensionsObjectTest : HashSetExtensionsTestBase<HashSetExtensionTestObject>;

    [InheritsTests]
    public sealed class HashSetExtensionsIntTest : HashSetExtensionsTestBase<int>;
}
