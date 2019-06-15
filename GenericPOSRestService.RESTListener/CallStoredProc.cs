using GenericPOSRestService.Common;
using GenericPOSRestService.Common.ServiceCallClasses;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace GenericPOSRestService.RESTListener
{
    public class CallStoredProc
    {
        OrderCreateRequest request;
        long menuParentId = 0;
        static int count = 0;

        public CallStoredProc(OrderCreateRequest request)
        {
            this.request = request;
            count = 0;
        }

        //empty constructor
        public CallStoredProc()
        {

        }

        /// <summary>
        /// Connect to the database 
        /// Run stored Procedures depending on what the items are
        /// </summary>
        public int StoredProcs()
        {
            int basketId = 0;            // Id of the current transaction
            int parentItemId = 0;        // value of the rows ID value
            int numOfItems = 0;          // Items in the order
            int numOfParentModItems = 0; // Number of Parent Modifier items
            int itemQty = 0;             //item Quantity
            long itemId = 0;             // Item Id
            int componentId = 0;

            // Create a new SqlConnection object
            using (SqlConnection con = new SqlConnection())
            {
                // Configure the SqlConnection object
                con.ConnectionString = RESTNancyModule.ConnectionString;
                con.Open();
                Log.Info("Connected to the Database");

                // 1) call the iOrderBasketAdd stored proc get the Id for the new Basket- this must be called first for a 
                //    new transaction
                basketId = IOrderBasketAdd(con, Convert.ToInt32(request.DOTOrder.RefInt), request.DOTOrder.Kiosk, string.Empty, 0);

                // 2) check how may items there are in the request
                numOfItems = request.DOTOrder.Items.Count;

                Log.Info($"The order has {numOfItems} item(s)");

                // 3) loop through each item and process each item as appropriate
               

                for (int i = 0; i < (numOfItems); i++)
                {
                    // Get the Id and quantity of each item
                    itemId = Convert.ToInt64(request.DOTOrder.Items[i].ID);  //long 1d of the item
                    itemQty = Convert.ToInt32(request.DOTOrder.Items[i].Qty); //the quantity of theItem


                    // i) you call add parent item for each meal deal or Single product then use the if statements for further processing 
                    //  
                    parentItemId = IOrderBasketAddParentItem(con, basketId, Convert.ToInt32(itemQty), itemId);

                    
                    //if item is a single Item and has a modifier run the iOrderBasketAddParentModifier
                    if (request.DOTOrder.Items[i].Items.Count == 1)
                    {
                        //load the main item with the parent item ID  and the modifier URN
                        IOrderBasketAddParentModifier(con, Convert.ToInt64(request.DOTOrder.Items[i].Items[0].ID), Convert.ToInt32(itemQty), parentItemId);
                    }

                    // this is the check for 20 dips and 3 modifiers
                    if (request.DOTOrder.Items[i].Items.Count == 3) 
                    {
                        //load the main item with the parent item ID and the modifier URN
                        for (int j = 0; j < 3; j++)
                            IOrderBasketAddParentModifier(con, Convert.ToInt64(request.DOTOrder.Items[i].Items[j].ID), Convert.ToInt32(request.DOTOrder.Items[i].Items[j].Qty), parentItemId);
                        break;
                    }

                    //Check for a meal with 2 sides i.e drink and Fries which are components
                    int numOfItemsInParent = request.DOTOrder.Items[i].Items.Count;

                    //If a meal or main item has other components loop through
                    if ((numOfItemsInParent != 0) && (count < 2)) 
                    {
                        menuParentId = Convert.ToInt64(request.DOTOrder.Items[0].ID);  //long 1d of the parent item

                        for (int k = 0; k < (numOfItemsInParent-1); k++)
                        {
                            itemId = Convert.ToInt64(request.DOTOrder.Items[i].Items[k].ID);
                            itemQty = Convert.ToInt32(request.DOTOrder.Items[i].Items[k].Qty);

                            //we don't want to check whether the Parent is a Component so just ignore it
                            //check it is a meal
                            if ((request.DOTOrder.Items[0].IsMenu == true) && (itemId != menuParentId))
                            {
                                //drink and side
                                if (count < 2)
                                {
                                    componentId = 0;

                                    //set the component Id to be used for the modifier
                                    componentId = IOrderBasketAddComponentItem(con, itemQty, parentItemId, itemId, componentId);

                                    //check if the component has a modifier
                                    if ((request.DOTOrder.Items[i].Items[k].Items.Count) > 0)
                                    {
                                        long componentModifierId = Convert.ToInt64(request.DOTOrder.Items[i].Items[k].Items[0].ID);
                                        IOrderBasketAddComponentModifier(con, itemQty, parentItemId, componentModifierId, componentId);
                                    }
                                    count++;
                                }
                            }
                        }
                    }

                }

                Log.Info("Disconnected from the Database");
            }
            return basketId;

        }

        /// <summary>
        /// call the iOrderBasketAdd stored proc get the Id for the new Basket
        /// </summary>
        /// <param name="con"></param>
        /// <returns>result from Stored procedure</returns>
        public int IOrderBasketAdd(SqlConnection con, int refInt, string kioskId, string checkBasketOrderId, long checkBasketSubTotal)
        {
            // create and configure a new command 
            SqlCommand com = con.CreateCommand();
            Random random = new Random();

            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "iOrderBasketAdd";

            SqlParameter p1 = com.CreateParameter();
            p1.ParameterName = "@kioskRefInt";
            p1.SqlDbType = SqlDbType.Int;
            p1.Value = refInt;
            com.Parameters.Add(p1);
        
            SqlParameter p2 = com.CreateParameter();
            p2.ParameterName = "@kioskID";
            p2.SqlDbType = SqlDbType.Int;
            p2.Value = kioskId;
            com.Parameters.Add(p2);

            SqlParameter p3 = com.CreateParameter();
            p3.ParameterName = "@checkBasketOrderId";
            p3.SqlDbType = SqlDbType.NVarChar;
            p3.Value = checkBasketOrderId;
            com.Parameters.Add(p3);

            SqlParameter p4 = com.CreateParameter();
            p4.ParameterName = "@checkBasketSubTotal ";
            p4.SqlDbType = SqlDbType.BigInt;
            p4.Value = checkBasketSubTotal;
            com.Parameters.Add(p4);

            var jsonResult = new StringBuilder();
            int basketId = 0;       

            // Execute the command and process the results
            using (var reader = com.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    jsonResult.Append("[]");
                }
                while (reader.Read())
                {
                    basketId = Convert.ToInt32(reader.GetValue(0));

                    //Log the result
                    Log.Info($"iOrderBasketAdd output: {basketId}");
                }
            }

            return basketId;
        }

        /// <summary>
        /// call the iOrderBasketAddParentItem stored proc get the Id for the parent
        /// </summary>
        /// <param name="con"></param>
        /// <returns>result from Stored procedure</returns>
        public int IOrderBasketAddParentItem(SqlConnection con, int basketId, int itemQty, long itemId)
        {
                if (basketId < 1)
                {
                    throw new Exception("Invalid BasketId");
                }

                // create and configure a new command 
                SqlCommand com = con.CreateCommand();

                com.CommandType = CommandType.StoredProcedure;
                com.CommandText = "iOrderBasketAddParentItem";

                int parentItemId = 0;
            
                 // Create SqlParameter objects 
                SqlParameter p1 = com.CreateParameter();
                p1.ParameterName = "@BasketID";
                p1.SqlDbType = SqlDbType.Int;
                p1.Value = basketId;
                com.Parameters.Add(p1);

                //URN to identify the main product URN
                SqlParameter p2 = com.CreateParameter();
                p2.ParameterName = "@AKDURN";
                p2.SqlDbType = SqlDbType.BigInt;
                p2.Value = itemId;
                com.Parameters.Add(p2);

                //URN to identify the main product Quantity
                SqlParameter p3 = com.CreateParameter();
                p3.ParameterName = "@quantity";
                p3.SqlDbType = SqlDbType.Int;
                p3.Value = itemQty;
                com.Parameters.Add(p3);

                 var jsonResult = new StringBuilder();

                // Execute the command and process the results
                using (var reader = com.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        jsonResult.Append("[]");
                    }
                    while (reader.Read())
                    {             
                        parentItemId = Convert.ToInt32(reader.GetValue(0));

                    //Display The details
                    jsonResult.Append(reader.GetValue(0).ToString());

                    Log.Info($"iOrderBasketAddParentItem output: {parentItemId}");
                    }

                return parentItemId;

                }
        }


        /// <summary>
        /// call the iOrderBasketAddComponentItem stored proc creates a new parent item for a standalone product or a meal deal
        /// </summary>
        /// <param name="con"></param>
        /// <returns>result from Stored procedure</returns>
        public int IOrderBasketAddComponentItem(SqlConnection con, int qty, int parentItemId, long itemId, int componentItemId)
            {
                // create and configure a new command 
                SqlCommand com = con.CreateCommand();
                int component = 0;

                com.CommandType = CommandType.StoredProcedure;
                com.CommandText = "iOrderBasketAddComponentItem";

                // Create SqlParameter objects 
                SqlParameter p1 = com.CreateParameter();
                p1.ParameterName = "@ParentItemID";
                p1.SqlDbType = SqlDbType.Int;
                p1.Value = parentItemId;
                com.Parameters.Add(p1);

                //URN to identify the main product URN
                SqlParameter p2 = com.CreateParameter();
                p2.ParameterName = "@AKDURN";
                p2.SqlDbType = SqlDbType.BigInt;
                p2.Value = itemId;
                com.Parameters.Add(p2);

                //URN to identify the main product Quantity
                SqlParameter p3 = com.CreateParameter();
                p3.ParameterName = "@quantity";
                p3.SqlDbType = SqlDbType.Int;
                p3.Value = qty;
                com.Parameters.Add(p3);

                //URN to identify the main product Quantity
                SqlParameter p4 = com.CreateParameter();
                p4.ParameterName = "@ComponentItemID";
                p4.SqlDbType = SqlDbType.Int;
                p4.Value = componentItemId;
                com.Parameters.Add(p4);

                var jsonResult = new StringBuilder();

                // Execute the command and process the results
                using (var reader = com.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        jsonResult.Append("[]");
                    }
                    while (reader.Read())
                    {
                        component = Convert.ToInt32(reader.GetValue(0));

                        //Log the result

                        //Display The details
                        jsonResult.Append(reader.GetValue(0).ToString());

                        Log.Info($"IOrderBasketAddComponentItem output: {component}");
                    }
                }

                return component;

            }


        /// <summary>
        /// call the iOrderBasketAddComponentItem stored proc creates a new parent item for a standalone product or a meal deal
        /// </summary>
        /// <param name="con"></param>
        /// <returns>result from Stored procedure</returns>
        public void IOrderBasketAddComponentModifier(SqlConnection con, int qty, int parentItemId, long componentModifierId, int? componentItemId)
        {
            // create and configure a new command 
            SqlCommand com = con.CreateCommand();

            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "IOrderBasketAddComponentModifier";

            // Create SqlParameter objects 
            SqlParameter p1 = com.CreateParameter();
            p1.ParameterName = "@ParentItemID";
            p1.SqlDbType = SqlDbType.Int;
            p1.Value = parentItemId;
            com.Parameters.Add(p1);

            //URN to identify the main product URN
            SqlParameter p2 = com.CreateParameter();
            p2.ParameterName = "@AKDURN";
            p2.SqlDbType = SqlDbType.BigInt;
            p2.Value = componentModifierId;
            com.Parameters.Add(p2);

            //URN to identify the main product Quantity
            SqlParameter p3 = com.CreateParameter();
            p3.ParameterName = "@quantity";
            p3.SqlDbType = SqlDbType.Int;
            p3.Value = qty;
            com.Parameters.Add(p3);

            //URN to identify the main product Quantity
            SqlParameter p4 = com.CreateParameter();
            p4.ParameterName = "@ComponentItemID";
            p4.SqlDbType = SqlDbType.Int;
            p4.Value = componentItemId;
            com.Parameters.Add(p4);

            var jsonResult = new StringBuilder();

            // Execute the command and process the results
            using (var reader = com.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    jsonResult.Append("[]");
                }
                while (reader.Read())
                {
                    int modifier = Convert.ToInt32(reader.GetValue(0));

                    //Log the result

                    //Display The details
                    jsonResult.Append(reader.GetValue(0).ToString());

                    Log.Info($"IOrderBasketAddComponentModifier output: {modifier}");
                }
            }

        }


        /// <summary>
        ///  Creates a new modifer item for a product other than a MealDeal
        /// </summary>
        /// <param name="con"></param>
        /// <returns>result from Stored procedure</returns>
        public void IOrderBasketAddParentModifier(SqlConnection con, long modifierId, int qty, int parentItemId )
        {
            // create and configure a new command 
            SqlCommand com = con.CreateCommand();
            int parentModifier = 0;

            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "IOrderBasketAddParentModifier";

            // Create SqlParameter objects 
            SqlParameter p1 = com.CreateParameter();
            p1.ParameterName = "@ParentItemID";
            p1.SqlDbType = SqlDbType.Int;
            p1.Value = parentItemId;
            com.Parameters.Add(p1);

            //URN to identify the main product URN
            SqlParameter p2 = com.CreateParameter();
            p2.ParameterName = "@AKDURN";
            p2.SqlDbType = SqlDbType.BigInt;
            p2.Value = modifierId;
            com.Parameters.Add(p2);

            //URN to identify the main product Quantity
            SqlParameter p3 = com.CreateParameter();
            p3.ParameterName = "@quantity";
            p3.SqlDbType = SqlDbType.Int;
            p3.Value = qty;
            com.Parameters.Add(p3);


            var jsonResult = new StringBuilder();

            // Execute the command and process the results
            using (var reader = com.ExecuteReader())
            {
                if (!reader.HasRows)
                {
                    jsonResult.Append("[]");
                }
                while (reader.Read())
                {
                   parentModifier = Convert.ToInt32(reader.GetValue(0));

                    //Log the result

                    //Display The details
                    jsonResult.Append(reader.GetValue(0).ToString());

                    Log.Info($"IOrderBasketAddParentModifier output: {parentModifier}");
                }
            }
        }

    }
}
