// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using AutoFixture;
using NSubstitute;
using NuGetUtility.Extensions;

namespace NuGetUtility.Test.Extensions
{
    public class EnumExtensionTest
    {
        private enum EnumWithoutDescription
        {
            Should,
            Fail,
        }

        private enum EnumWithPartialDescription
        {
            [System.ComponentModel.Description("Should")]
            Should,
            Fail,
        }

        private enum EnumWithDescriptions
        {
            [System.ComponentModel.Description("Should")]
            Should,
            [System.ComponentModel.Description("Pass")]
            Pass,
        }

        [SetUp]
        public void SetUp()
        {
        }

        [Test]
        public void AreAllColumnDescriptionsWritten()
        {
            var values = (EnumWithDescriptions[])Enum.GetValues(typeof(EnumWithDescriptions));

            var descriptions = values.Where(value => !string.IsNullOrWhiteSpace(value.GetDescription()))
                                     .ToArray();

            Assert.That(descriptions.Length, Is.EqualTo(values.Length));
             
        }

        [Test]
        public void SomeOrAllDescriptionsAreMissing(
            [Values(typeof(EnumWithPartialDescription),
                    typeof(EnumWithoutDescription))] Type type)
        {
            var values = Enum.GetValues(type).Cast<Enum>().ToArray();

            var descriptions = values.Where(value => !string.IsNullOrWhiteSpace((value).GetDescription()))
                                     .ToArray();

            Assert.That(descriptions.Length, Is.Not.EqualTo(values.Length));
        }

        [Test]
        public void DescriptionToEnumValueDeserialization()
        {
            Type enumType = typeof(EnumWithDescriptions);

            string[] invalidColumnNames = {
                nameof(EnumWithDescriptions.Should),
                nameof(EnumWithDescriptions.Pass),
                "Fail"
            };
            string[] validColumnNames = {
                nameof(EnumWithDescriptions.Should),
                nameof(EnumWithDescriptions.Pass),
            };

            try
            {
                _ = invalidColumnNames.Select(columnName => Enum.Parse(enumType, columnName, true));
            }
            catch (Exception e)
            {
                Assert.That(e.GetType(), Is.EqualTo(typeof(ArgumentException)));
            }

            try
            {
                object[] output = validColumnNames.Select(columnName => Enum.Parse(enumType, columnName, true)).ToArray();
                Assert.That(output.Length, Is.EqualTo(validColumnNames.Length));
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
        }



    }
}
