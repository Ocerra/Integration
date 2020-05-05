using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo.Models
{
    public class OdooCountry
    {
        public int Id { get; set; }
        public OdooString Code { get; set; }
    }

    public class OdooCurrency {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Currency_Unit_Label { get; set; }
    }

    public class OdooTax
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public string Type_Tax_Use { get; set; }

        public string Description { get; set; }

        public double Amount { get; set; }

        public string Cash_Basis_Base_Account_Id { get; set; }
    }

    public class OdooAccount {
        public int Id { get; set; }
        public string Name { get; set; }
        public OdooKeyValue Currency_Id { get; set; }
        public string Code { get; set; }
        public bool Deprecated { get; set; }
        public string Internal_Type { get; set; }
        public string Internal_Group { get; set; }

        public OdooKeyValue Parent_Id { get; set; }

        public OdooKeyValue User_Type { get; set; }
    }

    public class OdooCompany {
        public int Id { get; set; }

        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Name { get; set; }
        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Create_Date { get; set; }
        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Display_Name { get; set; }
        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Title { get; set; }
        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Vat { get; set; }
        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Website { get; set; }
        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Comment { get; set; }

        public bool Active { get; set; }
        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Email { get; set; }
        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Phone { get; set; }
        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Mobile { get; set; }
        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Company_Name { get; set; }

        [JsonConverter(typeof(NullableArrayJsonConverter))]
        public string[] Country_Id { get; set; }

        [JsonConverter(typeof(NullableArrayJsonConverter))]
        public string[] Currency_Id { get; set; }

        [JsonConverter(typeof(NullableArrayJsonConverter))]
        public string[] Property_Account_Payable_Id { get; set; }
    }

    public class OdooPurchaseOrder {
        public int Id { get; set; }

        public string Name { get; set; }
        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Origin { get; set; }
        [JsonConverter(typeof(NullableArrayJsonConverter))]
        public string[] Partner_Id { get; set; }
        [JsonConverter(typeof(NullableArrayJsonConverter))]
        public string[] Currency_Id { get; set; }

        [JsonConverter(typeof(NullableArrayJsonConverter))]
        public string[] User_Id { get; set; }

        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string State { get; set; }

        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Date_Order { get; set; }

        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Date_Approve { get; set; }

        public double? Amount_Untaxed{ get; set; }
        public double? Amount_Tax { get; set; }
        public double? Amount_Total { get; set; }
    }

    public class OdooBill
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("name")]
        public OdooString Name { get; set; }

        [JsonProperty("invoice_sequence_number_next")]
        public OdooString InvoiceSequenceNumberNext { get; set; }

        [JsonProperty("date")]
        public OdooDate Date { get; set; }
        [JsonProperty("ref")]
        public OdooString Ref { get; set; }
        [JsonProperty("state")]
        public OdooString State { get; set; }
        [JsonProperty("type")]
        public OdooString Type { get; set; }
        [JsonProperty("journal_id")]
        public OdooKeyValue JournalId { get; set; }
        [JsonProperty("company_id")]
        public OdooKeyValue CompanyId { get; set; }
        [JsonProperty("company_currency_id")]
        public OdooKeyValue CompanyCurrencyId { get; set; }
        [JsonProperty("currency_id")]
        public OdooKeyValue CurrencyId { get; set; }
        [JsonProperty("partner_id")]
        public OdooKeyValue PartnerId { get; set; }
        [JsonProperty("commercial_partner_id")]
        public OdooKeyValue CommercialPartnerId { get; set; }
        [JsonProperty("invoice_user_id")]
        public OdooKeyValue InvoiceUserId { get; set; }
        [JsonProperty("user_id")]
        public OdooKeyValue UserId { get; set; }
        [JsonProperty("invoice_line_ids")]
        public OdooArray<OdooBillLine> InvoiceLineIds { get; set; }
        [JsonProperty("line_ids")]
        public OdooArray<OdooBillLine> LineIds { get; set; }
        [JsonProperty("reversed_entry_id")]
        public OdooKeyValue ReversedEntryId { get; set; }
        [JsonProperty("amount_untaxed")]
        public OdooDecimal AmountUntaxed { get; set; }
        [JsonProperty("amount_tax")]
        public OdooDecimal AmountTax { get; set; }
        [JsonProperty("amount_total")]
        public OdooDecimal AmountTotal { get; set; }
        [JsonProperty("amount_residual")]
        public OdooDecimal AmountResidual { get; set; }
        [JsonProperty("amount_untaxed_signed")]
        public OdooDecimal AmountUntaxedSigned { get; set; }
        [JsonProperty("amount_tax_signed")]
        public OdooDecimal AmountTaxSigned { get; set; }
        [JsonProperty("amount_total_signed")]
        public OdooDecimal AmountTotalSigned { get; set; }
        [JsonProperty("amount_residual_signed")]
        public OdooDecimal AmountResidualSigned { get; set; }
        [JsonProperty("invoice_payment_state")]
        public OdooString InvoicePaymentState { get; set; }
        [JsonProperty("invoice_date")]
        public OdooDate InvoiceDate { get; set; }
        [JsonProperty("invoice_date_due")]
        public OdooDate InvoiceDateDue { get; set; }
        [JsonProperty("invoice_origin")]
        public OdooString InvoiceOrigin { get; set; }
        [JsonProperty("auto_post")]
        public bool AutoPost { get; set; }
    }

    public class OdooBillLine : IOdooLine
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("move_name")]
        public OdooString MoveName { get; set; }
        [JsonProperty("date")]
        public OdooDate Date { get; set; }
        [JsonProperty("ref")]
        public OdooString Ref { get; set; }
        [JsonProperty("journal_id")]
        public OdooKeyValue JournalId { get; set; }
        [JsonProperty("company_id")]
        public OdooKeyValue CompanyId { get; set; }
        [JsonProperty("company_currency_id")]
        public OdooKeyValue CompanyCurrencyId { get; set; }
        [JsonProperty("account_id")]
        public OdooKeyValue AccountId { get; set; }
        [JsonProperty("account_internal_type")]
        public OdooString AccountInternalType { get; set; }
        [JsonProperty("sequence")]
        public OdooString Sequence { get; set; }
        [JsonProperty("name")]
        public OdooString Name { get; set; }
        [JsonProperty("quantity")]
        public OdooDecimal Quantity { get; set; }
        [JsonProperty("price_unit")]
        public OdooDecimal PriceUnit { get; set; }
        [JsonProperty("discount")]
        public OdooDecimal Discount { get; set; }
        [JsonProperty("debit")]
        public OdooDecimal Debit { get; set; }
        [JsonProperty("credit")]
        public OdooDecimal Credit { get; set; }
        [JsonProperty("balance")]
        public OdooDecimal Balance { get; set; }
        [JsonProperty("amount_currency")]
        public OdooDecimal AmountCurrency { get; set; }
        [JsonProperty("price_subtotal")]
        public OdooDecimal PriceSubtotal { get; set; }
        [JsonProperty("price_total")]
        public OdooDecimal PriceTotal { get; set; }
        [JsonProperty("reconciled")]
        public bool Reconciled { get; set; }
        [JsonProperty("blocked")]
        public bool Blocked { get; set; }
        [JsonProperty("currency_id")]
        public OdooKeyValue CurrencyId { get; set; }
        [JsonProperty("partner_id")]
        public OdooKeyValue PartnerId { get; set; }
        [JsonProperty("product_id")]
        public OdooKeyValue ProductId { get; set; }
        [JsonProperty("payment_id")]
        public OdooKeyValue PaymentId { get; set; }
        [JsonProperty("create_date")]
        public OdooKeyValue CreateDate { get; set; }
        [JsonProperty("write_date")]
        public OdooDate Write_Date { get; set; }
        [JsonProperty("purchase_line_id")]
        public OdooKeyValue PurchaseLineId { get; set; }
        [JsonProperty("tax_line_id")]
        public OdooKeyValue TaxLineId { get; set; }
        [JsonProperty("tax_group_id")]
        public OdooKeyValue TaxGroupId { get; set; }
        [JsonProperty("tax_repartition_line_id")]
        public OdooKeyValue TaxRepartitionLineId { get; set; }
        [JsonProperty("exclude_from_invoice_tab")]
        public bool ExcludeFromInvoiceTab { get; set; }

        [JsonProperty("tax_ids")]
        public List<long> TaxIds { get; set; }
    }

    public class OdooProduct {
        public long Id { get; set; }
        
        [JsonProperty("default_code")]
        public string DefaultCode { get; set; }

        public string Name { get; set; }
        public bool Active { get; set; }

        [JsonProperty("currency_price")]
        public OdooDecimal CurrencyPrice { get; set; }
    }
}
