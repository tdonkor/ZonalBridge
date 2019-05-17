using GenericPOSRestService.Common.ServiceCallClasses;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.IO;

namespace GenericPOSRestService.RESTListener
{
    public class ZonalWrapper
    {
        RestClient client;
        RestRequest request;

        RequestDetails details;
        static long count = 1;
      
        public ZonalWrapper()
        {
            details = new RequestDetails();
        }

        public long OrderPosNum()
        {
            count++;
            return count;
            
        }

        public OrderCreatePOSResponse CheckBasket(OrderCreateRequest orderRequest, OrderCreatePOSResponse orderResponse)
        {
            //get the header details
            details.HeaderInformation(out client, out request);

            string checkBasketStr = "{\"request\": {\"method\":\"checkBasket\"," +
                 "\"bundleIdentifier\" : " +
                 "\"Acrelec\"" +
                 ", \"userDeviceIdentifier\" : " +
                 orderRequest.DOTOrder.Kiosk + 
                 ", \"platform\" : " + "\"" + RESTNancyModule.Platform + "\"" +
                ", \"siteId\": " +
                RESTNancyModule.SiteId + ", " +
                 "\"salesAreaId\" : " +
                RESTNancyModule.SalesAreaId +
                ", \"ServiceId\" : " +
                 1 +
                ", \"lines\" : [{" +
                 "\"IngredientId\" : " +
                orderRequest.DOTOrder.Items[0].ID + 
                 ", \"portionTypeId\" : " +
                 1 +
                 ", \"displayRecordId\" : " +
                  178284 +
                 ", \"quantity\" : " +
                    1 +
                 ", \"courseId\" : " +
                     123 +
                 ", \"menuId\" : " +
                  RESTNancyModule.MenuId +
                "}]" +
                "}" +
                "}";

            //execute the checkbasket body
            request.AddParameter("request", checkBasketStr);

            //generate the response
            IRestResponse response = client.Execute(request);
         
           string basketStr = JsonConvert.SerializeObject(response.Content, Formatting.Indented);

            //prepare the class for conversion
            dynamic basketData = JsonConvert.DeserializeObject<dynamic>(response.Content);

            orderResponse.OrderCreateResponse.Order.Kiosk = orderRequest.DOTOrder.Kiosk;
            orderResponse.OrderCreateResponse.Order.RefInt = orderRequest.DOTOrder.RefInt;
          

            //remove decimal point from total returned and put in the response Amount due.
            string AmountDue = Convert.ToString(basketData.basketTotal);

            string AmountDueWithoutDecimal = AmountDue.Replace(".", string.Empty);
            orderResponse.OrderCreateResponse.Order.Totals.AmountDue = Convert.ToInt64(AmountDueWithoutDecimal);

            orderResponse.OrderCreateResponse.Order.OrderID = basketData.basketId;

            if (string.IsNullOrEmpty(orderResponse.OrderCreateResponse.Order.OrderID))
            {
                orderResponse.OrderCreateResponse.Order.Reason = basketData.response;
            }

            return orderResponse;
        }

        public OrderCreatePOSResponse PlacePaidOrder(OrderCreateRequest orderRequest, OrderCreatePOSResponse orderResponse)
        {
            details.HeaderInformation(out client, out request);

            orderRequest.DOTOrder.OrderID = File.ReadAllText("C:\\Test\\BasketId.txt");

            string paidOrderStr = "{\"request\" : {\"method\" : \"placePaidOrder\", " +
             "\"bundleIdentifier\" : " + "\" \"" +
            ", \"userEmailAddress\" : " + "\"Dan@Acrelec.co.uk\"" +
            ", \"userDeviceIdentifier\" : " + "\"Kiosk1\"" +
             ", \"siteId\": " + RESTNancyModule.SiteId +
            ", \"salesAreaId\" : " + RESTNancyModule.SalesAreaId +
           ", \"TransactionId\" : " + "\"" + orderRequest.DOTOrder.RefInt + "\"" + //use refId
           ", \"basketId\" : " + "\"" + orderRequest.DOTOrder.OrderID + "\"" + // get from CheckBasket make it the orderID in the request
            ", \"table\" : " + "\"\"" +
            ", \"deviceData\" : " + "\" \"" +
              ", \"platform\" : " + "\"" + RESTNancyModule.Platform + "\"" +
           "}}";


           // File.WriteAllText("C:\\temp\\Tester.json", paidOrderStr);
            //execute the paidOrder
            request.AddParameter("request", paidOrderStr);

            //generate the response
            IRestResponse response = client.Execute(request);

            //Expose the class details
             dynamic paidOrder =  JsonConvert.DeserializeObject<dynamic>(response.Content);
         

            //
            //Build response with request Items that the response needs
            //
            orderResponse.OrderCreateResponse.Order.Kiosk = orderRequest.DOTOrder.Kiosk;
            orderResponse.OrderCreateResponse.Order.RefInt = orderRequest.DOTOrder.RefInt;
            orderResponse.OrderCreateResponse.Order.OrderID = orderRequest.DOTOrder.OrderID;



            if (string.IsNullOrEmpty(orderResponse.OrderCreateResponse.Order.OrderID))
            {
                orderResponse.OrderCreateResponse.Order.Reason = paidOrder.response;
            }

            orderResponse.OrderCreateResponse.Order.Totals.AmountPaid = orderRequest.DOTOrder.Tender.Total;
            orderResponse.OrderCreateResponse.Order.OrderPOSNumber = paidOrder.accountNumber;

            return orderResponse;

        }

            
    }
}
