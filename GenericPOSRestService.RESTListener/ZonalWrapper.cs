using GenericPOSRestService.Common.ServiceCallClasses;
using Newtonsoft.Json;
using RestSharp;
using System;

namespace GenericPOSRestService.RESTListener
{
    public class ZonalWrapper
    {
        RestClient client;
        RestRequest request;

        RequestDetails details;
      
        public ZonalWrapper()
        {
            details = new RequestDetails();
        }

        public OrderCreatePOSResponse CheckBasket(OrderCreateRequest orderRequest, OrderCreatePOSResponse orderResponse)
        {

            details.HeaderInformation(out client, out request);

            //copy the details to the Acrelec Order Response
            //get the venue information
            string checkBasketStr = "{\"request\": {\"method\":\"checkBasket\"," +
                 "\"bundleIdentifier\" : " +
                 "\"Acrelec\"" +
                 ", \"userDeviceIdentifier\" : " +
                 "\"Kiosk 1\"" +
                 ", \"platform\" : " + "\"" + RESTNancyModule.Platform + "\"" +
                ", \"siteId\": " +
                RESTNancyModule.SiteId + ", " +
                 "\"salesAreaId\" : " +
                RESTNancyModule.SalesAreaId +
                ", \"ServiceId\" : " +
                 1 +
                ", \"lines\" : [{" +
                 "\"IngredientId\" : " +
                  "\"10000000219\"" +
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

            //execute the venue body
            request.AddParameter("request", checkBasketStr);

            //generate the response
            IRestResponse response = client.Execute(request);
         

            //get the Menu data as a string
            string basketStr = JsonConvert.SerializeObject(response.Content, Formatting.Indented);

            //prepare the class for conversion
            dynamic basketData = JsonConvert.DeserializeObject<dynamic>(response.Content);

            //
            //Build response with request Items that the response needs
            //
          

            orderResponse.OrderCreateResponse.Order.Kiosk = orderRequest.DOTOrder.Kiosk;
            orderResponse.OrderCreateResponse.Order.RefInt = orderRequest.DOTOrder.RefInt;
        
            //remove decimal point from total returned and put in the response Amount due.
            string AmountDue = Convert.ToString(basketData.basketTotal);

            string AmountDueWithoutDecimal = AmountDue.Replace(".", string.Empty);

            orderResponse.OrderCreateResponse.Order.OrderID = basketData.basketId;
            orderResponse.OrderCreateResponse.Order.Totals.AmountDue = Convert.ToInt64(AmountDueWithoutDecimal);
            return orderResponse;
        }

        public void PlacePaidOrder()
        {
            details.HeaderInformation(out client, out request);


            string paidOrderStr = "{\"request\": {\"method\":\"placePaidOrder\"," +
                " \"siteId\": " +
                RESTNancyModule.SiteId + ", " +
                 "\"salesAreaId\" : " +
                RESTNancyModule.SalesAreaId +
                ", \"TransactionId\" : " + // use refInt
                " \"Test00001 \"" + 
                ", \"basketId\" : " +  // get from CheckBasket make it the orderID
                 "\"1E3EECAF-FB41-EAFF-EEB6433C3662F86F\"" +
                 ", \"table\" : " +
                 "\"1\"" +
                 ", \"deviceData\" : " +
                 "\"\"" +
                  ", \"platform\" : " + "\"" + RESTNancyModule.Platform + "\"" +
                "}}";

            //execute the paidOrder
            request.AddParameter("request", paidOrderStr);

            //generate the response
            IRestResponse response = client.Execute(request);

            //Expose the class details
             dynamic paidOrder =  JsonConvert.DeserializeObject<dynamic>(response.Content);
            
            //copy the details to the Acrelec Order Response



    }

    void CallStoredProcedure()
        {

        }

        void PopulateAcrelecBasketOrderResponse()
        {

        }

        void PopulateAcrelecPaymentResponse()
        {
        }
            
    }
}
