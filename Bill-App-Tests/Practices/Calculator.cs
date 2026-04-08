namespace Bill_App_Tests.Practices;

// This is just for learning xUnit, not part of the real project
public class Calculator
{
    public int Sum(int a, int b) => a + b;

    public int Subtract(int a, int b) => a - b;

    public double Divide(int a, int b)
    {
        if (b == 0) throw new DivideByZeroException("Cannot divide by zero");
        return (double)a / b;
    }

    public bool IsEven(int number) => number % 2 == 0;
}
