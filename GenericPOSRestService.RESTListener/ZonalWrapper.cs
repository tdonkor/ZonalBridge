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
        CallStoredProc storedProcs;

        public ZonalWrapper()
        {
            details = new RequestDetails();
            storedProcs = new CallStoredProc();
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

            //execute the check basket request
            request.AddParameter("request", payLoad);

            //generate the response
            IRestResponse response = client.Execute(request);

            //prepare the class for conversion
            dynamic basketData = JsonConvert.DeserializeObject<dynamic>(response.Content);

            orderResponse.OrderCreateResponse.Order.Kiosk = orderRequest.DOTOrder.Kiosk;
            orderResponse.OrderCreateResponse.Order.RefInt = orderRequest.DOTOrder.RefInt;

            //remove decimal point from total returned and put in the response Amount due.
            string AmountDue = Convert.ToString((basketData.basketTotal) * 100);

            string AmountDueWithoutDecimal = AmountDue.Replace(".", string.Empty);
            orderResponse.OrderCreateResponse.Order.Totals.AmountDue = Convert.ToInt64(AmountDueWithoutDecimal);

            //check for a discount
            //int total = 0;

            //for (int i = 0; i < basketData.lines[i].count; i++)
            //{
            //    total += Convert.ToInt64(basketData.lines[i].amount);
            //}


            orderResponse.OrderCreateResponse.Order.OrderID = basketData.basketId;

            //check order ID is not empty or Nul
            if (string.IsNullOrEmpty(orderResponse.OrderCreateResponse.Order.OrderID))
            {
                orderResponse.OrderCreateResponse.Order.Reason = basketData.response;
                // the OrderId parameter was not specified
                orderResponse.SetPOSError(Errors.OrderIDNotSpecified);
            }
            else
            {
                //open a connection
                using (SqlConnection con = new SqlConnection())
                {
                    // Configure the SqlConnection object
                    con.ConnectionString = RESTNancyModule.ConnectionString;
                    con.Open();
                    Log.Info("Connected to the Database");

                    //update to the BasketTable.
                    storedProcs.IOrderBasketAdd(con, Convert.ToInt32(orderRequest.DOTOrder.RefInt), orderRequest.DOTOrder.Kiosk, orderResponse.OrderCreateResponse.Order.OrderID, orderResponse.OrderCreateResponse.Order.Totals.AmountDue);
                }
                Log.Info("Disconnected from the Database");
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
            //Header details
            details.HeaderInformation(out client, out request);

            //call Database get BasketID and RefInt from procedure and compare.
            Log.Info($"Get OrderID Value from Database for RefInt{orderRequest.DOTOrder.RefInt}");
            AKDiOrderBasket  orderBasket = GetOrderBasketId(orderRequest.DOTOrder.RefInt, orderRequest.DOTOrder.Kiosk);
            orderRequest.DOTOrder.OrderID = orderBasket.CheckBasketOrderID;

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
            orderResponse.OrderCreateResponse.Order.Totals.AmountPaid = Convert.ToInt64(orderBasket.CheckBasketSubTotal);
            orderResponse.OrderCreateResponse.Order.OrderPOSNumber = paidOrder.accountNumber;

            return orderResponse;

        }


        /// <summary>
        /// Use to return/store Basket/OrderID from a table for the stored order after a Function 33 
        /// and use the values for a Function 3  
        /// </summary>
        /// <param name="con"></param>
        public AKDiOrderBasket GetOrderBasketId(string KioskRefInt, string kioskId)
        {
            AKDiOrderBasket orderBasket = new AKDiOrderBasket();

            // Create a new SqlConnection object
            using (SqlConnection con = new SqlConnection())
            {
                // Configure the SqlConnection object
                con.ConnectionString = RESTNancyModule.ConnectionString;
                con.Open();
                Log.Info("Connected to the Database");

                // create and configure a new command 
                SqlCommand com = new SqlCommand(
                    $"select KioskRefInt, KioskID, CheckBasketOrderID, CheckBasketSubTotal from {RESTNancyModule.TableName} where KioskRefInt = @kioskRefInt and KioskId = @kioskID",
                    con);

                // Create SqlParameter objects 
                SqlParameter p1 = com.CreateParameter();
                p1.ParameterName = "@kioskRefInt";
                p1.SqlDbType = SqlDbType.Int;
                p1.Value = Convert.ToInt32(KioskRefInt);
                com.Parameters.Add(p1);

                SqlParameter p2 = com.CreateParameter();
                p2.ParameterName = "@kioskId";
                p2.SqlDbType = SqlDbType.Int;
                p2.Value = Convert.ToInt32(kioskId);
                com.Parameters.Add(p2);

                var reader = com.ExecuteReader();

                // Execute the command and process the results
                while (reader.Read())
                {
                    orderBasket.KioskRefInt = Int64.Parse(reader["KioskRefInt"].ToString());
                    orderBasket.KioskID = int.Parse(reader["KioskID"].ToString());
                    orderBasket.CheckBasketOrderID = reader["CheckBasketOrderID"].ToString();
                    orderBasket.CheckBasketSubTotal = int.Parse(reader["CheckBasketSubTotal"].ToString());
                }

            }

           return orderBasket;
        }
     }
        /// <summary>
        /// Items returned from the IOrderBasket
        /// </summary>
        public class AKDiOrderBasket
        {
            public int ID { get; set; }

            public long? KioskRefInt { get; set; }

            public int? KioskID { get; set; }

            public string CheckBasketOrderID { get; set; }

            public int? CheckBasketSubTotal { get; set; }

        }


}

