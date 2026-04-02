// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using AutoFixture;
using NuGetUtility.Extensions;

namespace NuGetUtility.Test.Extensions
{
    [GenerateGenericTest(typeof(string))]
    [GenerateGenericTest(typeof(HashSetExtensionTestObject))]
    [GenerateGenericTest(typeof(int))]
    internal class HashSetExtensionsTest<T>
    {
        [Before(Test)]
        public void SetUp()
        {
            _uut = new HashSet<T>(new Fixture().CreateMany<T>());
        }

        private HashSet<T>? _uut;

        [Test]
        public async Task AddMany_Should_AddNewElementsToHashSet()
        {
            T[] newElements = new Fixture().CreateMany<T>().ToArray();
            var initialElements = _uut!.ToImmutableList();
            _uut!.AddRange(newElements);

            await Assert.That(_uut).IsEquivalentTo(initialElements.AddRange(newElements).Distinct(), CollectionOrdering.Any);
        }

        [Test]
        public async Task AddMany_Should_OnlyAddNewItems()
        {
            T[] newElements = new Fixture().CreateMany<T>().ToArray();
            var initialElements = _uut!.ToImmutableList();
            _uut!.AddRange(initialElements.AddRange(newElements));

            await Assert.That(_uut).IsEquivalentTo(initialElements.AddRange(newElements).Distinct(), CollectionOrdering.Any);
        }

        [Test]
        public async Task AddMany_Should_KeepSameHashSetIfOnlyAddingSameElements()
        {
            var initialElements = _uut!.ToImmutableList();
            _uut!.AddRange(initialElements);
            _uut!.AddRange(initialElements);
            _uut!.AddRange(initialElements);

            await Assert.That(_uut).IsEquivalentTo(initialElements, CollectionOrdering.Any);
        }
    }
}
