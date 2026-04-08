using System;
using System.Collections.Generic;
using System.Text;

namespace Bill_App_Tests.Practices;

public class OrderService(IPriceService priceService, IOrderLogger logger)
{
    public int CalculateTotal(string productName, int quantity)
    {
        var price = priceService.GetPrice(productName);
        var total = price * quantity;
        logger.Log($"Product: {productName}, Total: {total}");
        return total;
    }
}
