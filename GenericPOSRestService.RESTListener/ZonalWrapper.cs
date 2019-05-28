using GenericPOSRestService.Common.ServiceCallClasses;
using GenericPOSRestService.Common;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;

namespace GenericPOSRestService.RESTListener
{
    /// <summary>
    /// Methods for the zonal system
    /// </summary>
    public class ZonalWrapper
    {
        RestClient client;
        RestRequest request;
        RequestDetails details;

        public ZonalWrapper()
        {
            details = new RequestDetails();
        }

        /// <summary>
        /// Checks the Status of the POS
        /// </summary>
        /// <param name="orderResponse"></param>
        /// <returns></returns>
        public StatusPOSResponse AuthCheck(StatusPOSResponse orderResponse)
        {

            //get the header details
            details.HeaderInformation(out client, out request);

            string authCheckStr = "{\"request\": {\"method\":\"authCheck\"," +
              "\"platform\" : " + "\"" + RESTNancyModule.Platform + "\"" + " }}";

            //execute the Auth Check body
            request.AddParameter("request", authCheckStr);

            //generate the response
            IRestResponse response = client.Execute(request);

            //prepare the class for conversion
            dynamic authData = JsonConvert.DeserializeObject<dynamic>(response.Content);

            orderResponse.StatusResponse.Status = authData.Response = authData.response;

            DateTime time = DateTime.Now;
            orderResponse.StatusResponse.BusinessDay = time.ToString("yyMMddHH");

            if (orderResponse.StatusResponse.Status != "OK")
            {
                orderResponse.StatusResponse.TechnicalReason = authData.code + " : " + authData.detail;
            }

            return orderResponse;

        }

        /// <summary>
        /// Runs the CheckBasket API returns the response from the IOrder CheckBasket API
        /// </summary>
        /// <param name="orderRequest"></param>
        /// <param name="orderResponse"></param>
        /// <returns>OrderCreatePOSResponse response</returns>
        public OrderCreatePOSResponse CheckBasket(OrderCreateRequest orderRequest, OrderCreatePOSResponse orderResponse, string payLoad)
        {
            //get the header details
            details.HeaderInformation(out client, out request);

            //string checkBasketStr = "{\"request\": {\"method\":\"checkBasket\"," +
            //     "\"bundleIdentifier\" : " +
            //     "\"Acrelec\"" +
            //     ", \"userDeviceIdentifier\" : " +
            //     orderRequest.DOTOrder.Kiosk +
            //     ", \"platform\" : " + "\"" + RESTNancyModule.Platform + "\"" +
            //    ", \"siteId\": " +
            //    RESTNancyModule.SiteId + ", " +
            //     "\"salesAreaId\" : " +
            //    RESTNancyModule.SalesAreaId +
            //    ", \"ServiceId\" : " +
            //     1 +
            //    ", \"lines\" : [{" +
            //     "\"IngredientId\" : " +
            //    orderRequest.DOTOrder.Items[0].ID +
            //     ", \"portionTypeId\" : " +
            //     1 +
            //     ", \"displayRecordId\" : " +
            //      178284 +
            //     ", \"quantity\" : " +
            //        1 +
            //     ", \"courseId\" : " +
            //         123 +
            //     ", \"menuId\" : " +
            //      RESTNancyModule.MenuId +
            //    "}]" +
            //    "}" +
            //    "}";

            //execute the check basket request
           // request.AddParameter("request", checkBasketStr);
            request.AddParameter("request", payLoad);


            //generate the response
            IRestResponse response = client.Execute(request);

            // string basketStr = JsonConvert.SerializeObject(response.Content, Formatting.Indented);

            //prepare the class for conversion
            dynamic basketData = JsonConvert.DeserializeObject<dynamic>(response.Content);

            orderResponse.OrderCreateResponse.Order.Kiosk = orderRequest.DOTOrder.Kiosk;
            orderResponse.OrderCreateResponse.Order.RefInt = orderRequest.DOTOrder.RefInt;

            //remove decimal point from total returned and put in the response Amount due.
            string AmountDue = Convert.ToString(basketData.basketTotal);

            string AmountDueWithoutDecimal = AmountDue.Replace(".", string.Empty);
            orderResponse.OrderCreateResponse.Order.Totals.AmountDue = Convert.ToInt64(AmountDueWithoutDecimal);

            orderResponse.OrderCreateResponse.Order.OrderID = basketData.basketId;

            //check order ID is not empty or Null
            if (string.IsNullOrEmpty(orderResponse.OrderCreateResponse.Order.OrderID))
            {
                orderResponse.OrderCreateResponse.Order.Reason = basketData.response;
                // the OrderId parameter was not specified
                orderResponse.SetPOSError(Errors.OrderIDNotSpecified);
            }
            else
            {
                //TODO call stored procedure store BasketID and REFInt from procedure and compare.
                // Log.Info($"Store OrderID Value{orderRequest.DOTOrder.OrderID} for RefInt value:{orderRequest.DOTOrder.RefInt}");


                //TODOstore Order Id value - remove after testing
               // File.WriteAllText("C:\\Temp\\BasketId.txt", orderResponse.OrderCreateResponse.Order.OrderID);
;

                //TODO call the CheckBasket Store Procedure to get checkBasket Json string.
                //Log.Info("Get CheckBasket string from Database");
                ExecuteNonQueryExample(orderResponse.OrderCreateResponse.Order.OrderID, orderResponse.OrderCreateResponse.Order.RefInt);

            }

            return orderResponse;
        }

        /// <summary>
        /// Runs the PlacePaidOrder API returns the response from the IOrder PlacePaidOrder API
        /// </summary>
        /// <param name="orderRequest"></param>
        /// <param name="orderResponse"></param>
        /// <returns>OrderCreatePOSResponse response</returns>
        public OrderCreatePOSResponse PlacePaidOrder(OrderCreateRequest orderRequest, OrderCreatePOSResponse orderResponse)
        {
            details.HeaderInformation(out client, out request);

            //TODO remove just in for testing- read orderId from file.
           // orderRequest.DOTOrder.OrderID = File.ReadAllText("C:\\Temp\\BasketId.txt");

            //call Database get BasketID and RefInt from procedure and compare.
            Log.Info($"Get OrderID Value from Database for RefInt{orderRequest.DOTOrder.RefInt}");
            orderRequest.DOTOrder.OrderID = ExecuteScalarExample(orderRequest.DOTOrder.RefInt);

            string paidOrderStr = "{\"request\" : {\"method\" : \"placePaidOrder\", " +
             "\"bundleIdentifier\" : " + "\" \"" +
            ", \"userEmailAddress\" : " + "\"Acrelec@Acrelec.co.uk\"" +
            ", \"userDeviceIdentifier\" : " + orderRequest.DOTOrder.Kiosk +
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
            dynamic paidOrder = JsonConvert.DeserializeObject<dynamic>(response.Content);

            //
            //Build response with Kiosk, REFInt and Orde ID Values
            //
            orderResponse.OrderCreateResponse.Order.Kiosk = orderRequest.DOTOrder.Kiosk;
            orderResponse.OrderCreateResponse.Order.RefInt = orderRequest.DOTOrder.RefInt;
            orderResponse.OrderCreateResponse.Order.OrderID = orderRequest.DOTOrder.OrderID;

            if (string.IsNullOrEmpty(orderResponse.OrderCreateResponse.Order.OrderID))
            {
                orderResponse.OrderCreateResponse.Order.Reason = paidOrder.response;
                // the OrderId parameter was not specified
                orderResponse.SetPOSError(Errors.OrderIDNotSpecified);
            }

            //Build response with AmountPaid and OrderPOSNumber
            orderResponse.OrderCreateResponse.Order.Totals.AmountPaid = orderRequest.DOTOrder.Tender.Total;
            orderResponse.OrderCreateResponse.Order.OrderPOSNumber = paidOrder.accountNumber;

            return orderResponse;

        }


        /// <summary>
        /// Store BasketId and RefId
        /// </summary>
        /// <param name="orderIdValue"></param>
        /// <param name="refIntValue"></param>
        public static void ExecuteNonQueryExample(string orderIdValue, string refIntValue)
        {
            // Create a new SqlConnection object
            using (SqlConnection con = new SqlConnection())
            {
                // 1. Configure the SqlConnection object
                con.ConnectionString = RESTNancyModule.ConnectionString;

                // Open the database connection and execute the example 
                // commands through the connection
                con.Open();
              
                // using (SqlCommand cmd = new SqlCommand("INSERT INTO BASKETDATATABLE (BasketId, RefInt) VALUES (@OrderId, @RefInt ) ", con))
                using (SqlCommand cmd = new SqlCommand($"UPDATE {RESTNancyModule.TableName} SET BasketId = @OrderId, RefInt = @RefInt;", con))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.AddWithValue("@OrderId", orderIdValue);
                    cmd.Parameters.AddWithValue("@RefInt", refIntValue);

                    int result = cmd.ExecuteNonQuery();

                    if (result == 1)
                    {
                        Log.Info("BasketDataTable.BasketId updated");
                    }
                    else
                    {
                        Log.Info("BasketDataTable.BasketId not updated");
                    }
                }
            }
        }


        /// <summary>
        /// Use to return Basket/OrderID from a table for the stored order after a Function 33 
        /// and for a Function 3  
        /// </summary>
        /// <param name="con"></param>
        public string ExecuteScalarExample(string RefInt)
        {
            string orderId = string.Empty;

            // Create a new SqlConnection object
            using (SqlConnection con = new SqlConnection())
            {
                // 1. Configure the SqlConnection object
                con.ConnectionString = RESTNancyModule.ConnectionString;

                // Open the database connection and execute the example 
                // commands through the connection
                con.Open();
                
                SqlCommand com = con.CreateCommand();
                com.CommandType = CommandType.Text;
                com.Parameters.Add("RefInt", SqlDbType.VarChar).Value = RefInt;

                com.CommandText = $"Select BasketId from {RESTNancyModule.TableName} WHERE RefInt = RefInt";

                // Execute the command and cast the result.

                 orderId = (string)com.ExecuteScalar();

                Log.Info("OrderId: {0}", orderId);

            }

            return orderId;
        }
     }
   }

