using System;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

public class SaleSession
{
    private readonly object _sync = new();

    public UserDto? Manager { get; private set; }
    public StoreOption? Store { get; private set; }
    public PaymentType PaymentType { get; private set; } = PaymentType.CashWithReceipt;

    public bool HasContext => Manager is not null && Store is not null;

    public void Reset()
    {
        lock (_sync)
        {
            Manager = null;
            Store = null;
            PaymentType = PaymentType.CashWithReceipt;
        }
    }

    public void SetContext(UserDto manager, StoreOption store)
    {
        if (manager is null) throw new ArgumentNullException(nameof(manager));
        if (store is null) throw new ArgumentNullException(nameof(store));

        lock (_sync)
        {
            Manager = manager;
            Store = store;
        }
    }

    public void SetPaymentType(PaymentType paymentType)
    {
        lock (_sync)
        {
            PaymentType = paymentType;
        }
    }
}
