using System;
using System.Collections.Generic;
using System.Text;

namespace Bill_App_Tests.Practices;

public interface IPriceService
{
    int GetPrice(string productName);
}
