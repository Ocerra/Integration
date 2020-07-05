using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
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

        [JsonProperty("property_account_payable_id")]
        public OdooKeyValue Property_Account_Payable_Id { get; set; }

        [JsonProperty("property_account_expense_categ")]
        public OdooKeyValue PropertyAccountExpenseCateg { get; set; }

        [JsonProperty("property_stock_account_input_categ")]
        public OdooKeyValue PropertyStockAccountInputCateg { get; set; }
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


        [JsonProperty("user_id")]
        public OdooKeyValue User_Id { get; set; }

        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string State { get; set; }

        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Date_Order { get; set; }

        [JsonConverter(typeof(NullableStringJsonConverter))]
        public string Date_Approve { get; set; }

        [JsonProperty("order_line")]
        public List<long> OrderLines { get; set; }

        public double? Amount_Untaxed{ get; set; }
        public double? Amount_Tax { get; set; }
        public double? Amount_Total { get; set; }
        
        [JsonConverter(typeof(NullableArrayJsonConverter))]
        public string[] invoice_ids { get; set; }

        [JsonProperty("validator")]
        public OdooKeyValue Validator { get; set; }

        public List<long> Message_Ids { get; set; }
    }


    public class OdooPurchaseOrderLine
    {
        /*
         account_analytic_id: false
        attribute1: [91, "FL Parts Sales"]
        attribute2: [26, "FLIGHTLINE"]
        attribute3: false
            company_id: [3, "Oceania Aviation Limited"]
        condition_id: [2, "NE"]
        date_planned: "2020-05-05"
        display_uom: [12, "Each"]
            id: 115813
            name: "[12345] Test Loading"
            price_subtotal: 20
        price_unit: 20
            product_id: [104865, "[12345] Test Loading"]
            product_qty: 1
        product_uom: [12, "Each"]
        quantity_available: 0
            state: "confirmed"
        taxes_id: [3] */


        public int Id { get; set; }

        public string Name { get; set; }

        public OdooDecimal Price_Subtotal { get; set; }
        public OdooDecimal Product_Qty { get; set; }

        [JsonProperty("company_id")]
        public OdooKeyValue CompanyId { get; set; }

        public string State { get; set; }

        [JsonProperty("product_id")]
        public OdooKeyValue ProductId { get; set; }

        //?
        [JsonProperty("attribute1")]
        public OdooKeyValue Attribute1 { get; set; }

        //Brand
        [JsonProperty("attribute2")]
        public OdooKeyValue Attribute2 { get; set; }

        //Division?
        [JsonProperty("attribute3")]
        public OdooKeyValue Attribute3 { get; set; }

        [JsonProperty("taxes_id")]
        public OdooKeyValue Taxes {get; set;}
    }

    public class OdooPurchaseOrderDetails {
        public long DraftInvoiceId { get; set; }
        public string Reference { get; set; }
    }

    public class OdooPurchaseOrderLineAttributes
    {
        public long? DivisionId { get; set; }
        public string DivisionCode { get; set; }
        
        public long? BrandId { get; set; }
        public string BrandCode { get; set; }

        public long? TaxId { get; set; }
        public string TaxCode { get; set; }
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


        [JsonProperty("origin")]
        public OdooString Origin { get; set; }
        [JsonProperty("date_invoice")]
        public OdooDate DateInvoiceV8 { get; set; }
        [JsonProperty("date_due")]
        public OdooDate DueDateV8 { get; set; }
        [JsonProperty("supplier_invoice_number")]
        public OdooString SupplierInvoiceNumberV8 { get; set; }

        /// <summary>
        /// Using number field instead of origin
        /// </summary>
        [JsonProperty("number")]
        public OdooString Number { get; set; }

        [JsonProperty("account_id")]
        public OdooKeyValue AccountIdV8 { get; set; }
        [JsonProperty("invoice_line")]
        public OdooArray<OdooBillLineV8> InvoiceLineIdsV8 { get; set; }
        [JsonProperty("tax_line")]
        public OdooArray<OdooTaxLine> TaxLinesV8 { get; set; }
        [JsonProperty("check_total")]
        public OdooDecimal AmountTotalV8 { get; set; }
        


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
        
        [JsonProperty("residual")]
        public OdooDecimal Residual { get; set; }

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

    public class OdooBillLineTax : IOdooLine
    {
        public long Id { get; set; }
    }

    public class OdooBillLineV8 : IOdooLine
    {
        [JsonProperty("id")]
        public long Id { get; set; }        
        [JsonProperty("company_id")]
        public OdooKeyValue CompanyId { get; set; }
        [JsonProperty("account_id")]
        public OdooKeyValue AccountId { get; set; }
        [JsonProperty("sequence")]
        public int? Sequence { get; set; }
        [JsonProperty("name")]
        public OdooString Name { get; set; }
        [JsonProperty("quantity")]
        public OdooDecimal Quantity { get; set; }
        [JsonProperty("price_unit")]
        public OdooDecimal PriceUnit { get; set; }
        [JsonProperty("discount")]
        public OdooDecimal Discount { get; set; }
        [JsonProperty("price_subtotal")]
        public OdooDecimal PriceSubtotal { get; set; }
        [JsonProperty("product_id")]
        public OdooKeyValue ProductId { get; set; }
        [JsonProperty("invoice_line_tax_id")]
        public List<List<object>> TaxLineIdsV8 { get; set; }
        [JsonProperty("amount_total")]
        public OdooDecimal AmountTotal { get; set; }
        [JsonProperty("amount_untaxed")]
        public OdooDecimal AmountUntaxed { get; set; }

        [JsonProperty("attribute1")]
        public OdooKeyValue DevisionId { get; set; }

        [JsonProperty("attribute2")]
        public OdooKeyValue BrandId { get; set; }

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

        [JsonProperty("invoice_line_tax_id")]
        public List<long> TaxLineIdsV8 { get; set; }
        
    }

    public class OdooTaxLine : IOdooLine
    {
        /*
        account_analytic_id: false
        account_id: [1721, "21325 GST Paid"]
        amount: 1.04
        base: 6.96
        base_amount: 6.96
        factor_base: 1
        factor_tax: 1
        id: 359155
        name: "Standard Rate Purchases (15.0%)"
        tax_amount: 1.04 */

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("account_id")]
        public OdooKeyValue AccountId { get; set; }

        [JsonProperty("amount")]
        public OdooDecimal Amount { get; set; }

        [JsonProperty("tax_amount")]
        public OdooDecimal TaxAmount { get; set; }

        [JsonProperty("base")]
        public OdooDecimal Base { get; set; }

        [JsonProperty("base_amount")]
        public OdooDecimal BaseAmount { get; set; }

        [JsonProperty("factor_base")]
        public OdooDecimal FactorBase { get; set; }

        [JsonProperty("factor_tax")]
        public OdooDecimal FactorTax { get; set; }

        


        
    }

    public class OdooProductCategory
    {
        public long Id { get; set; }
        public string Name { get; set; }
        
        [JsonProperty("property_account_expense_categ")]
        public OdooKeyValue PropertyAccountExpense { get; set; }

        [JsonProperty("property_stock_account_input_categ")]
        public OdooKeyValue PropertyStockAccountInput { get; set; }

    }

    public class OdooProduct {
        public long Id { get; set; }
        
        [JsonProperty("default_code")]
        public string DefaultCode { get; set; }

        public string Name { get; set; }
        public bool Active { get; set; }

        [JsonProperty("currency_price")]
        public OdooDecimal CurrencyPrice { get; set; }

        [JsonProperty("property_account_expense")]
        public OdooKeyValue PropertyAccountExpense { get; set; }

        [JsonProperty("property_stock_account_input")]
        public OdooKeyValue PropertyStockAccountInput { get; set; }

        [JsonProperty("categ_id")]
        public OdooKeyValue ProductCategory { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

    }

    public class OdooUser {
        public long Id { get; set; }

        public string login { get; set; }
    }

    public class OdooMessage {
        public long Id { get; set; }

        public string Subject { get; set; }

        public string Body { get; set; }

        public OdooKeyValue Author_Id { get; set; }

        public string Email_From { get; set; }

        public string Reply_To { get; set; }

        [JsonProperty("date")]
        public OdooDate Date { get; set; }

    }
}
