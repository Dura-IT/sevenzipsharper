using System;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop
{
    [TestOf(typeof(PropVariant))]
    public sealed class PropVariantTests
    {
        [Test]
        public void IsEmpty_True_ForDefault()
        {
            PropVariant pv = new PropVariant();

            pv.IsEmpty.Should().BeTrue();
        }

        [Test]
        public void ToUInt32_ReturnsValue_ForVtUi4()
        {
            PropVariant pv = PropVariant.FromUInt32(42u);

            pv.ToUInt32().Should().Be(42u);
        }

        [Test]
        public void ToUInt32_ReturnsNull_WhenNotUi4()
        {
            PropVariant pv = PropVariant.FromUInt64(42ul);

            pv.ToUInt32().Should().BeNull();
        }

        [Test]
        public void ToUInt64_ReturnsValue_ForVtUi8()
        {
            PropVariant pv = PropVariant.FromUInt64(1_234_567_890_123ul);

            pv.ToUInt64().Should().Be(1_234_567_890_123ul);
        }

        [Test]
        public void ToInt32_ReturnsValue_ForVtI4()
        {
            PropVariant pv = PropVariant.FromInt32(-7);

            pv.ToInt32().Should().Be(-7);
        }

        [Test]
        public void ToBoolean_ReturnsTrue_ForVariantTrue()
        {
            PropVariant pv = PropVariant.FromBoolean(true);

            pv.ToBoolean().Should().BeTrue();
        }

        [Test]
        public void ToBoolean_ReturnsFalse_ForVariantFalse()
        {
            PropVariant pv = PropVariant.FromBoolean(false);

            pv.ToBoolean().Should().BeFalse();
        }

        [Test]
        public void ToBoolean_ReturnsNull_WhenNotBool()
        {
            PropVariant pv = PropVariant.FromUInt32(0u);

            pv.ToBoolean().Should().BeNull();
        }

        [Test]
        public void ToStringValue_ReturnsString_ForVtBstr()
        {
            PropVariant pv = PropVariant.FromString("hello");
            try
            {
                pv.ToStringValue().Should().Be("hello");
            }
            finally
            {
                pv.Clear();
            }
        }

        [Test]
        public void ToStringValue_ReturnsNull_ForEmpty()
        {
            PropVariant pv = new PropVariant();

            pv.ToStringValue().Should().BeNull();
        }

        [Test]
        public void ToDateTime_ReturnsCorrectUtc_ForVtFileTime()
        {
            DateTime expected = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            PropVariant pv = PropVariant.FromFileTime((ulong)expected.ToFileTimeUtc());

            pv.ToDateTime().Should().Be(expected);
        }

        [Test]
        public void ToDateTime_ReturnsNull_WhenNotFileTime()
        {
            PropVariant pv = PropVariant.FromUInt64(1000ul);

            pv.ToDateTime().Should().BeNull();
        }

        [Test]
        public void Clear_WhenPopulated_SetsVarTypeToEmpty()
        {
            PropVariant pv = PropVariant.FromUInt32(99u);

            pv.Clear();

            pv.IsEmpty.Should().BeTrue();
        }

        [Test]
        public void Clear_WhenHoldingBstr_FreesAndSetsEmpty()
        {
            PropVariant pv = PropVariant.FromString("temporary");

            pv.Clear();

            pv.IsEmpty.Should().BeTrue();
            pv.ToStringValue().Should().BeNull();
        }

        [Test]
        public void VarType_IsVtUInt32_AfterFromUInt32()
        {
            PropVariant pv = PropVariant.FromUInt32(1u);

            pv.VarType.Should().Be(PropVariant.VtUInt32);
        }

        [Test]
        public void VarType_IsVtUInt64_AfterFromUInt64()
        {
            PropVariant pv = PropVariant.FromUInt64(1ul);

            pv.VarType.Should().Be(PropVariant.VtUInt64);
        }

        [Test]
        public void VarType_IsVtFileTime_AfterFromFileTime()
        {
            PropVariant pv = PropVariant.FromFileTime(1ul);

            pv.VarType.Should().Be(PropVariant.VtFileTime);
        }
    }
}
