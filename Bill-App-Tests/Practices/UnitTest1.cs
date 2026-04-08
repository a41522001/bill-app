using Moq;

namespace Bill_App_Tests.Practices;

public class UnitTest1
{
    private readonly Calculator _calculator = new();
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(-1, 5, 4)]
    [InlineData(0, 0, 0)]
    public void Sum_ShouldReturnExpected(int a, int b, int expect)
    {
        // Act    — 執行要測的東西
        var actual = _calculator.Sum(a, b);
        // Assert — 驗證結果對不對
        Assert.Equal(expect, actual);
    }
    [Theory]
    [InlineData(1, 3, -2)]
    [InlineData(8, 3, 5)]
    [InlineData(-9, 2, -11)]
    public void Subtract_ShouldReturnExpected(int a, int b, int expect)
    {
        // Act
        var actual = _calculator.Subtract(a, b);
        // Assert
        Assert.Equal(expect, actual);
    }
    [Theory]
    [InlineData(2, true)]
    [InlineData(-2, true)]
    [InlineData(-3, false)]
    public void IsEven_ShouldReturnExpected(int input, bool expect)
    {
        // Act
        var actual = _calculator.IsEven(input);
        // Assert
        Assert.Equal(expect, actual);
    }
    [Fact]
    public void Divide_ShouldThrow_WhenDivisorIsZero()
    {

        // Act & Assert
        Assert.Throws<DivideByZeroException>(() => _calculator.Divide(2, 0));
    }
    [Theory]
    [InlineData(3, 30)]

    public void OrderService_Test(int quantity, int expect)
    {
        var mockPriceSevice = new Mock<IPriceService>();
        mockPriceSevice.Setup(p => p.GetPrice("Apple")).Returns(10);
        var mockLogger = new Mock<IOrderLogger>();
        var orderService = new OrderService(mockPriceSevice.Object, mockLogger.Object);
        var actual = orderService.CalculateTotal("Apple", quantity);
        mockLogger.Verify(m => m.Log($"Product: Apple, Total: {actual}"), Times.Once());
        Assert.Equal(expect, actual);
    }
}
