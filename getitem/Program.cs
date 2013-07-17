﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using GW2Miner.Engine;
using GW2Miner.Domain;

namespace getitem
{
    static class Program
    {
        static TradeWorker trader = new TradeWorker();
        static double BLSalvageCost = 0;

        // returns true - ok
        //         false - error
        static bool SalvageOrSell(Item item, double salvageCost, out bool toSalvage, double chanceToGetUpgrade)
        {
            toSalvage = false;

            if (!item.IsRich || (item.TypeId != TypeEnum.Armor && item.TypeId != TypeEnum.Weapon)) return false;

            Item upgrade = trader.get_upgrade(item, null);

            if (upgrade == null) return false;

            double upgradePrice = chanceToGetUpgrade * upgrade.MaxOfferUnitPrice * 0.85;
            double itemPrice = Math.Max(item.MaxOfferUnitPrice * 0.85, item.VendorPrice);

            if (item.MinLevel >= 68 && item.RarityId >= RarityEnum.Rare)
            {
                Task<List<Item>> ectoItemList = trader.get_items(19721);
                int ectoPrice = ((Item)ectoItemList.Result[0]).MinSaleUnitPrice;
                Console.WriteLine("Ecto price: {0}", ectoPrice);

                // assume 1 ecto per salvage
                itemPrice = itemPrice - ectoPrice;
            }

            if (upgradePrice >= (itemPrice + salvageCost))
            {
                toSalvage = true;
                Console.WriteLine("Salvage!  Upgrade Price Profit: {0} Sale Profit: {1}", upgradePrice - salvageCost, itemPrice);
            }
            else
            {
                Console.WriteLine("Sell.  Sale Profit: {0} Upgrade price profit: {1}", itemPrice, upgradePrice - salvageCost);
            }

            return true;
        }

        static int GetMySellingPrice(int itemId)
        {
            List<Item> sellList = trader.get_my_sells().Result;
            foreach (Item item in sellList)
            {
                if (item.Id == itemId)
                {
                    return item.UnitPrice;
                }
            }

            return -1;
        }

        static int ReadKey()
        {
            while (true)
            {
                ConsoleKeyInfo choice = Console.ReadKey(true);
                if (char.IsDigit(choice.KeyChar))
                {
                    int answer = Convert.ToInt32(choice.KeyChar);
                    return answer - 48; //-48 because 0 is represented in unicode by 48 and 1 by 49 etc etc
                }
                //Console.WriteLine("\nSorry, you need to input a number");
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string itemName;
            if (args.Count() == 0)
            {
                Console.WriteLine("Enter item name: ");
                itemName = Console.ReadLine();
            }
            else itemName = args[0];

            try
            {
                BLSalvageCost = trader.BlackLionKitSalvageCost;
                Console.WriteLine("BL Salvage Cost: {0}", BLSalvageCost);

                List<Item> itemList = trader.search_items(itemName, false).Result;

                if (itemList.Count > 0)
                {
                    uint count = 0;
                    foreach (Item item in itemList)
                    {
                        if (String.Compare(itemName, item.Name, true) != 0) continue;

                        Item richItem = trader.make_rich_item(item).Result;
                        int margin = richItem.MinSaleUnitPrice - richItem.MaxOfferUnitPrice;
                        double tpSalePrice = richItem.MaxOfferUnitPrice * 0.85;
                        richItem.setConsoleColor();
                        Console.WriteLine("");
                        Console.WriteLine("{0}) {1}: \"{2}\" TP Sale Profit: {3} Margin: {4}", count, richItem.Id, richItem.Name, tpSalePrice, margin);
                        Console.WriteLine("Description: {0}", richItem.Description);
                        Console.WriteLine("Rarity: {0}", richItem.RarityDescription);
                        Console.WriteLine("Level: {0}", richItem.MinLevel);
                        Console.WriteLine("Max Offer Unit Price: {0}", richItem.MaxOfferUnitPrice);
                        Console.WriteLine("Min Sale Unit Price: {0}", richItem.MinSaleUnitPrice);
                        Console.WriteLine("Offer Availability: {0}", richItem.BuyCount);
                        Console.WriteLine("Sale Availability: {0}", richItem.SellCount);
                        Console.WriteLine("Vendor Price: {0}", richItem.VendorPrice);

                        if (richItem.IsRich)
                        {
                            Console.WriteLine("Price Last Changed: {0}", richItem.PriceLastChanged);
                            Console.WriteLine("% Offer Price Change Last Hour: {0}", richItem.OfferPriceChangedLastHour);
                            Console.WriteLine("% Sale Price Change Last Hour: {0}", richItem.SalePriceChangedLastHour);
                            Console.WriteLine("Type Id: {0}", richItem.TypeId);
                            Console.WriteLine("SubType Id: {0}", richItem.SubTypeDescription);
                            Console.WriteLine("If salvaging using Black Lion Salvage Kit...");

                            bool toSalvage;
                            if (!SalvageOrSell(richItem, BLSalvageCost, out toSalvage, 1.0))
                                Console.WriteLine("More Profitable to sell for {0}", Math.Max(item.MaxOfferUnitPrice * 0.85, item.VendorPrice));
                            else if (!toSalvage)
                            {
                                Console.WriteLine("If salvaging using Master Salvage Kit...");
                                if (!SalvageOrSell(richItem, trader.MasterKitSalvageCost, out toSalvage, 0.8))
                                    Console.WriteLine("More Profitable to sell for {0}", Math.Max(item.MaxOfferUnitPrice * 0.85, item.VendorPrice));
                            }
                        }

                        Console.ResetColor();
                        if (richItem.VendorPrice > tpSalePrice)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("More Profitable to sell to the Vendor than to the Trading Post!");
                            Console.ResetColor();
                        }

                        count++;
                    } // end of foreach (Item item in itemList)

                    int choice = 0;
                    if (count > 1)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("Select which item to view the sell listing :");
                        while ((choice = ReadKey()) >= count) ;
                    }

                    if (count > 0)
                    {
                        int boughtPrice = trader.GetMyBoughtPrice(itemList[choice], DateTime.Now); // get the most recently bought of such an item
                        int sellingPrice = GetMySellingPrice(itemList[choice].Id);
                        int breakEvenPrice = (int)Math.Ceiling(boughtPrice / 0.85);
                        if (boughtPrice >= 0)
                        {
                            Console.WriteLine("Found bought price = {0}.  BreakEven price = {1}", boughtPrice, breakEvenPrice);
                        }

                        List<ItemBuySellListingItem> itemSellListing = trader.get_sell_listings(itemList[choice].Id).Result;
                        foreach (ItemBuySellListingItem sellListing in itemSellListing)
                        {
                            if (sellListing.PricePerUnit < breakEvenPrice) Console.ForegroundColor = ConsoleColor.Red;
                            if (sellingPrice >= 0 && sellListing.PricePerUnit == sellingPrice) Console.Write(">>>>> ");
                            Console.WriteLine("{0} listed by {1} seller(s).  Unit Price: {2}", sellListing.NumberAvailable, sellListing.NumberOfListings, sellListing.PricePerUnit);
                            Console.ResetColor();
                        }
                    }
                    //else
                    //    Console.WriteLine("Sorry!  We can't find any {0}.", itemName);

                    Console.WriteLine("Hit ENTER to exit...");
                    Console.ReadLine();
                }
                else
                    Console.WriteLine("Sorry!  We can't find any {0}.", itemName);
            }
            catch (Exception e)
            {
                Console.WriteLine(ExceptionHelper.FlattenException(e));
            }
        }
    }
}
