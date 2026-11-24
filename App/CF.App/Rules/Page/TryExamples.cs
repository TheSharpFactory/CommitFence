namespace CF.App.Rules.Page;

/// <summary>
/// The code that "Load an example" puts in the box of Try it: one for each kind of artifact that can be tried on code alone, each with
/// several artifacts that differ in what rules are written about. A kind with no example (a usage needs the build) has no button.
/// </summary>
internal static class TryExamples
{
    private const string _ifStatement = """
        public void Send(Invoice invoice)
        {
            if(invoice == null)
            {
                return;
            }

            if(invoice.Lines.Count == 0)
                return;

            if(invoice.Total < 0)
            {
                log.Warn("Negative total");
                invoice.Total = 0;
            }
        }
        """;

    private const string _method = """
        internal class InvoiceMailer
        {
            public void Send(Invoice invoice)
            {
                Deliver(Compose(invoice));
            }

            internal bool CanSend(Invoice invoice)
            {
                return invoice.Total > 0;
            }

            private Message Compose(Invoice invoice)
            {
                return new Message(invoice.Number);
            }
        }
        """;

    private const string _class = """
        /// <summary>Sends invoices by mail.</summary>
        public sealed class InvoiceMailer
        {
            public void Send(Invoice invoice) { }
        }

        internal class InvoiceArchive
        {
            private void Purge(int days) { }
        }

        internal static partial class InvoiceRules
        {
            public static bool IsDue(Invoice invoice) { return invoice.Days > 30; }
        }
        """;

    /// <summary>
    /// The example for an artifact named in words ("if statement", "method", "class"), or null when there is none.
    /// </summary>
    public static string? For(string artifactWords)
    {
        return artifactWords switch
        {
            "if statement" => _ifStatement,
            "method" => _method,
            "class" => _class,
            _ => null
        };
    }
}
