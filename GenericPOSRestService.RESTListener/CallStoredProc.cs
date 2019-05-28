using GenericPOSRestService.Common;
using GenericPOSRestService.Common.ServiceCallClasses;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GenericPOSRestService.RESTListener
{
    public class CallStoredProc
    {
        OrderCreateRequest request;
        long parentId = 0;
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
        /// Run stored Procedures
        /// </summary>
        /// <param name="menuStr"></param>
        /// <param name="guid"></param>
        public int StoredProcs()
        {
            int basketId = 0;
            int parentItemId = 0;

            int itemQty = 0;
            long itemId = 0;

            int parentQty = 0;
            int componentId = 0;


            // Create a new SqlConnection object
            using (SqlConnection con = new SqlConnection())
            {
                // Configure the SqlConnection object
                con.ConnectionString = RESTNancyModule.ConnectionString;
                con.Open();
                Log.Info("Connected to the Database");

                // 1) call the iOrderBasketAdd stored proc get the Id for the new Basket
                basketId = iOrderBasketAdd(con);

                //1- you call add parent item for each meal deal or stand alone product for a meal deal 
                //2 -  you then have to call add component item twice
                //  once for the drink
                //  once for the side
                // if the drink has a modifier, you have to call add component modifier
                // with the id of the component you are adding the modifier to

                int num = request.DOTOrder.Items.Count;
                int numOfItemsInParent = request.DOTOrder.Items[0].Items.Count;
                 
                parentId = Convert.ToInt64(request.DOTOrder.Items[0].ID);  //long 1d of the item
                parentQty = Convert.ToInt32(request.DOTOrder.Items[0].Qty);

                //load the main item with the parent item
                parentItemId = iOrderBasketAddParentItem(con, basketId, parentQty, parentId);

                //if item is a single Item has a modifier run the iOrderBasketAddParentModifier
                //if item is a single Item and not a meal but has a modifier run the iOrderBasketAddParentModifier

                if ((request.DOTOrder.Items[0].IsMenu == false) && (request.DOTOrder.Items[0].Items.Count > 0))
                {
                    //load the main item with the parent item
                    parentItemId = IOrderBasketAddParentModifier(con, basketId, parentQty, parentId);
                }

                if ((numOfItemsInParent != 0) && (count < 2)) //a meal or main item has other items loop through
                { 
                    for (int i = 0; i < numOfItemsInParent-1; i++) 
                    {
                        // first item is a drink
                            itemId = Convert.ToInt64(request.DOTOrder.Items[0].Items[i].ID);
                            itemQty = Convert.ToInt32(request.DOTOrder.Items[0].Items[i].Qty);

                         

                        // 2) call the iOrderBasketAddParentItem stored procedure for
                        // stand alone products for a meal deal 
                        // iOrderBasketAddParentItem(con, basketId, itemQty, itemId);

                        if ((request.DOTOrder.Items[0].IsMenu == true) && (itemId != parentId))
                        {

                            if (count < 2)
                            {
                                //Drinks always first
                                //if (count == 0)
                                    componentId = 0;

                                componentId =  IOrderBasketAddComponentItem(con, itemQty, parentItemId, itemId, componentId);

                                //check for a modifier
                                if ((request.DOTOrder.Items[0].Items[i].Items.Count) > 0)
                                        IOrderBasketAddComponentModifier(con, itemQty, parentItemId, itemId, componentId);




                                count++;
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
        public int iOrderBasketAdd(SqlConnection con)
        {
            // create and configure a new command 
            SqlCommand com = con.CreateCommand();
            Random random = new Random();

            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "iOrderBasketAdd";
         

            SqlParameter p1 = com.CreateParameter();
            p1.ParameterName = "@KioskId";
            p1.SqlDbType = SqlDbType.Int;
            p1.Value = Convert.ToInt32(request.DOTOrder.Kiosk);
            com.Parameters.Add(p1);

            // Create SqlParameter objects 
            SqlParameter p2 = com.CreateParameter();
            p2.ParameterName = "@BasketURN";
            p2.SqlDbType = SqlDbType.BigInt;
            p2.Value = random.Next(100000, 10000000); ;
            com.Parameters.Add(p2);
   

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
        public int iOrderBasketAddParentItem(SqlConnection con, int basketId, int itemQty, long itemId)
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

           
            // return parentId;
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

                        Log.Info($"iOrderBasketAddParentItem output: {component}");
                    }
                }

            return component;

            }


        /// <summary>
        /// call the iOrderBasketAddComponentItem stored proc creates a new parent item for a standalone product or a meal deal
        /// </summary>
        /// <param name="con"></param>
        /// <returns>result from Stored procedure</returns>
        public void IOrderBasketAddComponentModifier(SqlConnection con, int qty, int parentItemId, long itemId, int? componentItemId)
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
                    int modifier = Convert.ToInt32(reader.GetValue(0));

                    //Log the result

                    //Display The details
                    jsonResult.Append(reader.GetValue(0).ToString());

                    Log.Info($"iOrderBasketAddParentItem output: {modifier}");
                }
            }

        }


        /// <summary>
        /// call the iOrderBasketAddComponentItem stored proc creates a new parent item for a standalone product or a meal deal
        /// </summary>
        /// <param name="con"></param>
        /// <returns>result from Stored procedure</returns>
        public int IOrderBasketAddParentModifier(SqlConnection con, int qty, int parentItemId, long itemId)
        {
            // create and configure a new command 
            SqlCommand com = con.CreateCommand();
            int parentModifier = 0;

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
            p2.Value = itemId;
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

                    Log.Info($"iOrderBasketAddParentItem output: {parentModifier}");
                }
            }
            return parentModifier;

        }

        public int GetNum(SqlConnection con, int parentItemId)
        {
         
            SqlCommand com = con.CreateCommand();
            com.CommandType = CommandType.Text;

            // Create SqlParameter objects 
            SqlParameter p1 = com.CreateParameter();
            p1.ParameterName = "@ParentItemID";
            p1.SqlDbType = SqlDbType.Int;
            p1.Value = parentItemId;
            com.Parameters.Add(p1);

            int num = 0;
       
            com.CommandText = "select id from iOrderBasketComponentItem where iOrderBasketComponentItem.ParentItemID = @ParentItemID and IsMealDealSize = 1";
            num = Convert.ToInt32(com.ExecuteScalar());
            return num;
        }
    }
}
