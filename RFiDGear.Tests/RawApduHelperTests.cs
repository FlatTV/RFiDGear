using RFiDGear.Infrastructure;
using Xunit;

namespace RFiDGear.Tests
{
    public class RawApduHelperTests
    {
        [Theory]
        [InlineData("5A F2 6D E5", new byte[] { 0x5A, 0xF2, 0x6D, 0xE5 })]
        [InlineData("5af26de5", new byte[] { 0x5A, 0xF2, 0x6D, 0xE5 })]
        [InlineData("0x5A, 0xF2-6D:E5", new byte[] { 0x5A, 0xF2, 0x6D, 0xE5 })]
        public void TryParseHex_AcceptsCommonNotations(string input, byte[] expected)
        {
            Assert.True(RawApduHelper.TryParseHex(input, out var bytes));
            Assert.Equal(expected, bytes);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("5A F")]
        [InlineData("ZZ")]
        public void TryParseHex_RejectsInvalidInput(string input)
        {
            Assert.False(RawApduHelper.TryParseHex(input, out _));
        }

        [Fact]
        public void WrapNativeDesfire_SelectApplication_AddsLcAndLe()
        {
            var apdu = RawApduHelper.WrapNativeDesfire(new byte[] { 0x5A, 0xF2, 0x6D, 0xE5 });
            Assert.Equal(new byte[] { 0x90, 0x5A, 0x00, 0x00, 0x03, 0xF2, 0x6D, 0xE5, 0x00 }, apdu);
        }

        [Fact]
        public void WrapNativeDesfire_WithoutData_OmitsLc()
        {
            var apdu = RawApduHelper.WrapNativeDesfire(new byte[] { 0x6A });
            Assert.Equal(new byte[] { 0x90, 0x6A, 0x00, 0x00, 0x00 }, apdu);
        }

        [Fact]
        public void BuildOutgoing_ApduAsEntered_LeavesBytesUntouched()
        {
            var bytes = new byte[] { 0x90, 0x5A, 0x00, 0x00, 0x03, 0xF2, 0x6D, 0xE5, 0x00 };
            Assert.Equal(bytes, RawApduHelper.BuildOutgoing(bytes, RawApduFraming.ApduAsEntered));
        }

        [Theory]
        [InlineData(new byte[] { 0xCD, 0x09 }, RawApduFraming.NativeDesfire, true)]
        [InlineData(new byte[] { 0xFC }, RawApduFraming.NativeDesfire, true)]
        [InlineData(new byte[] { 0x5A, 0xF2, 0x6D, 0xE5 }, RawApduFraming.NativeDesfire, false)]
        [InlineData(new byte[] { 0x90, 0xDF, 0x00, 0x00, 0x01, 0x09, 0x00 }, RawApduFraming.ApduAsEntered, true)]
        [InlineData(new byte[] { 0x90, 0x5A, 0x00, 0x00, 0x03, 0xF2, 0x6D, 0xE5, 0x00 }, RawApduFraming.ApduAsEntered, false)]
        public void IsPotentiallyDestructive_FlagsWritingCommands(byte[] bytes, RawApduFraming framing, bool expected)
        {
            Assert.Equal(expected, RawApduHelper.IsPotentiallyDestructive(bytes, framing));
        }

        [Theory]
        [InlineData(0x91, 0x00, "OPERATION_OK")]
        [InlineData(0x91, 0xAE, "AUTHENTICATION_ERROR")]
        [InlineData(0x91, 0x9D, "PERMISSION_DENIED")]
        [InlineData(0x91, 0xDE, "DUPLICATE_ERROR")]
        [InlineData(0x90, 0x00, "OK")]
        public void DescribeStatusWord_DecodesKnownStatuses(byte sw1, byte sw2, string expected)
        {
            Assert.Equal(expected, RawApduHelper.DescribeStatusWord(sw1, sw2));
        }

        [Fact]
        public void DescribeResponse_SeparatesPayloadAndStatus()
        {
            var text = RawApduHelper.DescribeResponse(new byte[] { 0x01, 0x02, 0x91, 0xAE });
            Assert.Contains("01 02", text);
            Assert.Contains("SW=91 AE", text);
            Assert.Contains("AUTHENTICATION_ERROR", text);
        }

        [Fact]
        public void DescribeResponse_Empty_IsReported()
        {
            Assert.Equal("(no response)", RawApduHelper.DescribeResponse(new byte[0]));
        }
    }
}
