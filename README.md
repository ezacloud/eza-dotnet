# Eza Cloud SDK for .NET (`Ezacloud.Sdk`)

Official .NET client library for [Eza Cloud](https://eza.co.ke): PayEdge M-Pesa STK push, transactional emails, and cryptographically signed webhook verification.

Zero third-party dependencies. Built exclusively on .NET standard libraries (`System.Net.Http`, `System.Security.Cryptography`, `System.Text.Json`). Compatible with .NET 8.0+ and .NET Standard 2.1 (supports C#, F#, and VB.NET).

---

## Installation

Install via NuGet:

```bash
dotnet add package Ezacloud.Sdk
```

---

## Quick Start

```csharp
using System;
using System.Threading.Tasks;
using Ezacloud.Sdk;

// Automatically auto-configures using EZA_API_KEY, EZA_EMAIL_KEY on Eza Cloud pods
using var eza = new EzaClient(apiKey: "eza_live_...");

// 1. Trigger an M-Pesa STK push via PayEdge
var tx = await eza.PayEdge.StkPushAsync(new StkPushRequest
{
    Phone = "254712345678",
    AmountKes = 1500,
    AccountReference = "INV1024",
    Description = "Order payment"
});
Console.WriteLine($"Payment pending: {tx.CheckoutRequestId}");

// 2. Send a transactional email
var mail = await eza.Emails.SendAsync(new SendEmailRequest
{
    From = "Shop <orders@mail.shop.co.ke>",
    To = new() { "wanjiku@gmail.com" },
    Subject = "Your receipt",
    Html = "<p>Thank you for your order!</p>"
});
Console.WriteLine($"Email dispatched: {mail.Id}");

// 3. Verify incoming webhook signatures (e.g. ASP.NET Core Controller)
var evt = Webhook.Verify(
    rawBody,
    Request.Headers["Eza-Signature"],
    Environment.GetEnvironmentVariable("EZA_WEBHOOK_SECRET")
);
Console.WriteLine($"Verified event: {evt.Type}");
```
