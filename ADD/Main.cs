﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Drawing;
using System.Security.Cryptography;
using System.Web;
using BitcoinNET.RPCClient;
using ADD.Tools;
using System.Threading;
using System.Windows.Media;
using System.Text.RegularExpressions;
using Secp256k1;
using System.Numerics;


namespace ADD
{
    public partial class Main : Form
    {

        public static Dictionary<string, byte> coinVersion;
        public static Dictionary<string, string> coinShortName;
        public static Dictionary<string, int> coinPayloadByteSize;
        public static Dictionary<string, string> coinPort;
        public static Dictionary<string, string> coinIP;
        public static Dictionary<string, string> coinUser;
        public static Dictionary<string, string> coinPassword;
        public static Dictionary<string, string> coinSigningAddress;
        public static Dictionary<string, string> coinTrackingAddress;
        public static Dictionary<string, decimal> coinTransactionFee;
        public static Dictionary<string, decimal> coinMinTransaction;
        public static Dictionary<string, int> coinTransactionSize;
        public static Dictionary<string, Boolean> coinGetRawSupport;
        public static Dictionary<string, Boolean> coinFeePerAddress;
        public static Dictionary<string, Boolean> coinEnabled;
        public static Dictionary<string, Boolean> coinEnableSigning;
        public static Dictionary<string, Boolean> coinEnableTracking;
        public static Dictionary<string, string> coinHelperUrl;
        IDictionary<string, decimal> allAccounts;
        Dictionary<string, IEnumerable<string>> coinLastMemoryDump;

        Decimal fileSize;
        int transactionsSearched = 0;
        int transactionsFound = 0;
        int msgId = 0;
        Boolean trustTrustedlistContent = false;
        Boolean blockBlockedListContent = false;
        Boolean blockUnSignedContent = false;
        Boolean blockUntrustedContent = false;
        HashSet<string> hashTrustedList = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> hashBlockedList = new HashSet<string>(StringComparer.Ordinal);
        static readonly object _batchLocker = new object();
        static readonly object _buildLocker = new object();
        GlyphTypeface glyphTypeface = new GlyphTypeface(new Uri("file:///C:\\WINDOWS\\Fonts\\Arial.ttf"));
        IDictionary<int, ushort> characterMap;
        string[] infoArray;
 
        public Main()
        {
            InitializeComponent();
            Startup();

        }

        public void Startup()
    {
            tmrProcessBatch.Start();
            characterMap = glyphTypeface.CharacterToGlyphMap;
            infoArray = "Apertus immutably stores and interprets data on blockchains.|Never build files or click links from sources you do not trust.|Click Help, then info for assistance.|Create a profile and start sharing your thoughts.|Adding #keywords makes it easier for people to discover and follow your content.|Press the CTRL Key while submitting your search to rebuild the cache.|Did you know you can search by Transaction ID, Address or Keyword on the Explore tab?".Split('|');

         }
        private char GetRandomDivider()
        {
            char[] chars = "\\/:*?\"><|".ToCharArray();
            Random r = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);
            int i = r.Next(chars.Length);
            System.Threading.Thread.Sleep(100);

            return chars[i];
        }

        private string GetURL(string url)
        {
            if (url.ToUpper().StartsWith("HTTP"))
            {
                return url;
            }

            return "http://" + url;
        }

        private string GetRandomBuffer(int BufferLength)
        {
            //Quick Fix to allow Keyword functionality  will eventually deprecate this call.
            const string allowedChars = "####################";
            char[] chars = new char[BufferLength];
            var rd = new Random();

            for (int i = 0; i < BufferLength; i++)
            {
                chars[i] = allowedChars[rd.Next(0, allowedChars.Length)];
            }

            return new string(chars);
        }

        private void processLedger(string processId)
        {

            System.IO.StreamReader readLGR = new System.IO.StreamReader("process\\" + processId + ".LGR");
            string line = "";
            string TransId = null;
            int lineCount = 0;
            while ((line = readLGR.ReadLine()) != null && lineCount <= 1)
            {
                if (TransId == null) { TransId = line; }
                lineCount++;
            }
            readLGR.Close();

            if (lineCount > 1)
            {
                CreateLedgerFile(coinPayloadByteSize[cmbCoinType.Text], GetRandomBuffer(coinPayloadByteSize[cmbCoinType.Text]), coinIP[cmbCoinType.Text], coinPort[cmbCoinType.Text], coinUser[cmbCoinType.Text], coinPassword[cmbCoinType.Text], cmbWalletLabel.Text, coinVersion[cmbCoinType.Text], coinMinTransaction[cmbCoinType.Text], "process\\" + processId + ".LGR", TransId, "");
            }
            else
            {
                lock (_buildLocker)
                {
                    CreateArchive(TransId, cmbCoinType.Text, true, true);
                }
            }

        }

        private void CreateLedgerFile(int PayloadByteSize, string Padding, string WalletRPCIP, string WalletRPCPort, string WalletRPCUser, string WalletRPCPassword, string WalletLabel, byte CoinVersion, decimal CoinMinTransaction, string FilePath = null, string ledgerId = null, string TextMessage = null)
        {

            String processId = Guid.NewGuid().ToString();
            byte[] arcPayloadBytes = new byte[PayloadByteSize + 1];
            byte[] arcPadding = UTF8Encoding.UTF8.GetBytes(Padding);
            arcPayloadBytes[0] = CoinVersion;
            int payloadBytePosition = 1;
            byte[] fileBytes = null;
            byte[] buffer = null;
            byte[] msgBytes = null;
            string cglText = "";
            Dictionary<string, decimal> toMany = new Dictionary<string, decimal>();
            Dictionary<string, decimal> lastTransaction = new Dictionary<string, decimal>();
            string lastTransactionID = null;
            string line;
            int tranCount = 1;
            int ledgerCount = 0;
            string transactionId = "";
            int fileCount = 0;
            int totalMsgSize = 0;


            CoinRPC b = new CoinRPC(new Uri(GetURL(WalletRPCIP) + ":" + WalletRPCPort), new NetworkCredential(WalletRPCUser, WalletRPCPassword));
            b.SetTXFee(coinTransactionFee[cmbCoinType.Text]);

            try
            {
                if (!FilePath.ToUpper().EndsWith(".ADD"))
                {
                    if (TextMessage.Length > 0)
                    {
                        msgBytes = Encoding.UTF8.GetBytes(TextMessage);
                        cglText = GetRandomDivider() + msgBytes.Length.ToString().PadLeft(coinPayloadByteSize[cmbCoinType.Text] - 2, '0') + GetRandomDivider();
                        totalMsgSize = msgBytes.Length + cglText.Length;
                    }

                    if (FilePath.Length > 0)
                    {


                        var mergeFiles = FilePath.Split(',');
                        

                        foreach (var f in mergeFiles)
                        {
                            //additional logic and padding to ensure text data begins at byte[0] to assist in future searching.
                            //files will have a few bytes of unsearchable content at the begining of each file due to the random delimiter set.
                            fileCount++;
                            var intCurrentSize = 0;
                            var readFileBytes = System.IO.File.ReadAllBytes(f);
                            if (fileBytes != null) { intCurrentSize = fileBytes.Length; }

                            if (ledgerId != null)
                            {
                                cglText = ledgerId + GetRandomDivider() + readFileBytes.Length.ToString() + GetRandomDivider();
                            }
                            else
                            {
                                int totalFileSize = Path.GetFileName(f).Length + readFileBytes.Length.ToString().Length + readFileBytes.Length + 2;
                                int filePadding = coinPayloadByteSize[cmbCoinType.Text] - (totalFileSize % coinPayloadByteSize[cmbCoinType.Text]);
                                if (fileCount == mergeFiles.Length &&  totalMsgSize > 0) 
                                {
                                    filePadding = filePadding + (coinPayloadByteSize[cmbCoinType.Text] - (totalMsgSize % coinPayloadByteSize[cmbCoinType.Text]));
                                }
                                
                                cglText = Path.GetFileName(f) + GetRandomDivider() + readFileBytes.Length.ToString().PadLeft(filePadding + readFileBytes.Length.ToString().Length, '0') + GetRandomDivider();
                            }

                            buffer = new byte[cglText.Length + intCurrentSize + readFileBytes.Length];
                            Encoding.UTF8.GetBytes(cglText).CopyTo(buffer, 0);
                            readFileBytes.CopyTo(buffer, cglText.Length);
                            if (fileBytes != null) { fileBytes.CopyTo(buffer, (readFileBytes.Length + cglText.Length)); }
                            fileBytes = buffer;

                        }

                        progressBar.Maximum = fileBytes.Length;
                        progressBar.Value = 1;
                        progressBar.Visible = true;
                        lblStatusInfo.Text = "Status: Converting primary data to wallet addresses.";

                    }

                    if (TextMessage.Length > 0)
                    {
                        cglText = GetRandomDivider() + msgBytes.Length.ToString().PadLeft(coinPayloadByteSize[cmbCoinType.Text] - 2, '0') + GetRandomDivider();
                        buffer = new byte[cglText.Length + msgBytes.Length];
                        Encoding.UTF8.GetBytes(cglText).CopyTo(buffer, 0);
                        msgBytes.CopyTo(buffer, cglText.Length);
                        msgBytes = buffer;

                        if (fileBytes != null)
                        {
                            buffer = new byte[msgBytes.Length + fileBytes.Length];
                            msgBytes.CopyTo(buffer, 0);
                            fileBytes.CopyTo(buffer, msgBytes.Length);
                            fileBytes = buffer;
                        }
                        else
                        {
                            fileBytes = msgBytes;
                        }
                    }
                    if (coinEnableSigning[cmbCoinType.Text] && coinSigningAddress[cmbCoinType.Text] != null && ledgerId == null)
                    {

                        //Sign and 0 pad the signature allowing the archive data to always begin at byte[0]. Allows for future file and keyword lookup
                        System.Security.Cryptography.SHA256 mySHA256 = SHA256Managed.Create();
                        byte[] hashValue = mySHA256.ComputeHash(fileBytes);
                        string signature = b.SignMessage(coinSigningAddress[cmbCoinType.Text], BitConverter.ToString(hashValue).Replace("-", String.Empty));
                        var sigBytes = Encoding.UTF8.GetBytes(signature);
                        int totalSigSize = sigBytes.Length + sigBytes.Length.ToString().Length + 5;
                        int zeroPadding = coinPayloadByteSize[cmbCoinType.Text] - (totalSigSize % coinPayloadByteSize[cmbCoinType.Text]);

                        cglText = "SIG" + GetRandomDivider() + sigBytes.Length.ToString().PadLeft(zeroPadding + sigBytes.Length.ToString().Length, '0') + GetRandomDivider();

                        buffer = new byte[cglText.Length + fileBytes.Length + sigBytes.Length];
                        Encoding.UTF8.GetBytes(cglText).CopyTo(buffer, 0);
                        sigBytes.CopyTo(buffer, cglText.Length);
                        if (fileBytes != null) { fileBytes.CopyTo(buffer, (sigBytes.Length + cglText.Length)); }
                        fileBytes = buffer;

                    }




                    System.IO.StreamWriter arcFile = new System.IO.StreamWriter("process\\" + processId + ".ADD", true);

                    for (int arcBytePosition = 0; arcBytePosition < fileBytes.Length; arcBytePosition++)
                    {

                        if (payloadBytePosition > PayloadByteSize)
                        {
                            arcFile.WriteLine(Base58.EncodeWithCheckSum(arcPayloadBytes));
                            payloadBytePosition = 1;
                            arcPayloadBytes = new byte[PayloadByteSize + 1];
                            arcPayloadBytes[0] = CoinVersion;
                        }

                        arcPayloadBytes[payloadBytePosition] = fileBytes[arcBytePosition];
                        payloadBytePosition++;
                        progressBar.PerformStep();


                    }

                    for (int i = payloadBytePosition; i < PayloadByteSize; i++)
                    {
                        arcPayloadBytes[i] = arcPadding[i];
                    }

                    arcFile.WriteLine(Base58.EncodeWithCheckSum(arcPayloadBytes));
                    arcFile.Close();
                    lblStatusInfo.Text = "Status: Saving " + fileBytes.Length.ToString() + " bytes of primary data to the blockchain.";
                    progressBar.Value = 1;
                    progressBar.Maximum = (fileBytes.Length + PayloadByteSize) / PayloadByteSize;
                    progressBar.PerformStep();


                }
                else
                {
                    processId = FilePath.ToUpper().Remove(0, FilePath.Length - 40).Replace(".ADD", "");
                }


                //Signing address should always be the last address in the array to allow for total file lookups.
                if (coinEnableSigning[cmbCoinType.Text] && coinSigningAddress[cmbCoinType.Text] != null)
                {
                    //allow tracking by putting a signature address on the end of the file
                    System.IO.StreamWriter arcSign = new System.IO.StreamWriter("process\\" + processId + ".ADD", true);
                    arcSign.WriteLine(coinSigningAddress[cmbCoinType.Text]);
                    arcSign.Close();
                }

                

                System.IO.StreamReader readARC = new System.IO.StreamReader("process\\" + processId + ".ADD");
                System.IO.StreamWriter arcLedger = new System.IO.StreamWriter("process\\" + processId + ".LGR", true);


                while ((line = readARC.ReadLine()) != null)
                {
                    try
                    {
                        toMany.Add(line, CoinMinTransaction);
                        progressBar.PerformStep();

                    }
                    catch
                    {
                        //Cannot send to the same address more than twice in any transaction
                        //If data is identical use previous transaction instead of archiving identical data.
                        if (lastTransaction.SequenceEqual(toMany))
                        { transactionId = lastTransactionID; }
                        else
                        {
                            transactionId = b.SendMany(WalletLabel, toMany);
                        }

                        arcLedger.WriteLine(transactionId);
                        arcLedger.Flush();
                        lastTransaction = new Dictionary<string, decimal>(toMany);
                        lastTransactionID = transactionId;
                        toMany.Clear();
                        toMany.Add(line, CoinMinTransaction);
                        progressBar.PerformStep();
                        tranCount = 0;
                        GetTransactionResponse transLookup = b.GetTransaction(transactionId);
                        //Wait for the wallet to catch up.
                        while (transLookup.confirmations < 1 && ledgerCount > (coinTransactionSize[cmbCoinType.Text] * 10))
                        {
                            System.Threading.Thread.Sleep(1000);
                            transLookup = b.GetTransaction(transactionId);

                        }
                        if (transLookup.confirmations > 0) { ledgerCount = 0; }


                    }


                    if (tranCount == coinTransactionSize[cmbCoinType.Text])
                    {
                        //Breaking transaction file into size specified in wallet settings
                        transactionId = b.SendMany(WalletLabel,toMany);
                        arcLedger.WriteLine(transactionId);
                        arcLedger.Flush();
                        lastTransaction = new Dictionary<string, decimal>(toMany);
                        lastTransactionID = transactionId;
                        toMany.Clear();
                        tranCount = 0;

                        GetTransactionResponse transLookup = b.GetTransaction(transactionId);
                        //Wait for the wallet to catch up.
                        while (transLookup.confirmations < 1 && ledgerCount > (coinTransactionSize[cmbCoinType.Text] * 10))
                        {
                            System.Threading.Thread.Sleep(1000);
                            transLookup = b.GetTransaction(transactionId);
                        }
                        if (transLookup.confirmations > 0) { ledgerCount = 0; }



                    }
                    tranCount++;
                    ledgerCount++;
                }
                if (toMany.Count > 0)
                {
                    //Catching the straglers
                    transactionId = b.SendMany(WalletLabel, toMany);
                    arcLedger.WriteLine(transactionId);
                    arcLedger.Flush();
                    lastTransaction = new Dictionary<string, decimal>(toMany);
                    lastTransactionID = transactionId;
                    toMany.Clear();
                    tranCount = 0;
                    GetTransactionResponse transLookup = b.GetTransaction(transactionId);
                    //Wait for the wallet to catch up.
                    while (transLookup.confirmations < 1 && ledgerCount > (coinTransactionSize[cmbCoinType.Text] * 10))
                    {
                        System.Threading.Thread.Sleep(1000);
                        transLookup = b.GetTransaction(transactionId);
                    }

                }
                arcLedger.Close();
                readARC.Close();
                processLedger(processId.ToString());


            }
            catch (Exception e)
            {
                lblStatusInfo.Text = "Error: " + e.Message;
                tmrStatusUpdate.Start();
                tmrProgressBar.Start();
                return;
            }
            lblStatusInfo.Text = "Status: Saved " + (progressBar.Maximum * PayloadByteSize) + " bytes of address data to the blockChain.";
            tmrStatusUpdate.Start();
            tmrProgressBar.Start();


        }

        private void btnArchive_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show("NOTICE: Files or messages with repetitive data will greatly increase the cost of archivng.", "Confirm Saving", MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                CreateLedgerFile(coinPayloadByteSize[cmbCoinType.Text], GetRandomBuffer(coinPayloadByteSize[cmbCoinType.Text]), coinIP[cmbCoinType.Text], coinPort[cmbCoinType.Text], coinUser[cmbCoinType.Text], coinPassword[cmbCoinType.Text], cmbWalletLabel.Text, coinVersion[cmbCoinType.Text], coinMinTransaction[cmbCoinType.Text], txtFileName.Text, null,txtMessage.Text);
            }
        }


        private void btnAttachFiles_Click(object sender, EventArgs e)
        {

            DialogResult result = attachFiles.ShowDialog();
            if (result == DialogResult.OK)
            {
                fileSize = (decimal)0;
                txtFileName.Text = String.Join(",", attachFiles.FileNames);

                foreach (var f in attachFiles.FileNames)
                {
                    var filebytes = new System.IO.FileInfo(f).Length;
                    fileSize = fileSize + filebytes;
                }

                updateEstimatedCost();

            }

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Directory.CreateDirectory("root");
            Directory.CreateDirectory("process");
            RefreshCoinTypes();
            RefreshTrustCache();
            tmrStatusUpdate.Start();
        }

        public void RefreshTrustCache()
        {
            if (!System.IO.File.Exists("trust.conf"))
            {
                System.IO.StreamWriter writeTrustConf = new StreamWriter("trust.conf");
                writeTrustConf.WriteLine("True True False False");
                writeTrustConf.Close();
            }

            System.IO.StreamReader readTrustConf = new System.IO.StreamReader("trust.conf");
            while (!readTrustConf.EndOfStream)
            {
                var trustSettings = readTrustConf.ReadLine().Split(' ');

                try
                {
                    trustTrustedlistContent = Convert.ToBoolean(trustSettings[0]);
                    blockBlockedListContent = Convert.ToBoolean(trustSettings[1]);
                    blockUnSignedContent = Convert.ToBoolean(trustSettings[2]);
                    blockUntrustedContent = Convert.ToBoolean(trustSettings[3]);
                }
                catch { }
            }
            readTrustConf.Close();

            hashTrustedList.Clear();
            if (System.IO.File.Exists("trust.txt"))
            {
                System.IO.StreamReader readTrust = new System.IO.StreamReader("trust.txt");
                while (!readTrust.EndOfStream)
                {
                    hashTrustedList.Add(readTrust.ReadLine());
                }
                readTrust.Close();
            }

            hashBlockedList.Clear();
            if (System.IO.File.Exists("block.txt"))
            {
                System.IO.StreamReader readBlock = new System.IO.StreamReader("block.txt");
                while (!readBlock.EndOfStream)
                {
                    hashBlockedList.Add(readBlock.ReadLine());
                }
                readBlock.Close();
            }

        }

        public void RefreshCoinTypes()
        {
            try
            {
                coinVersion = new Dictionary<string, byte>();
                coinShortName = new Dictionary<string, string>();
                coinPayloadByteSize = new Dictionary<string, int>();
                coinTransactionFee = new Dictionary<string, decimal>();
                coinMinTransaction = new Dictionary<string, decimal>();
                coinTransactionSize = new Dictionary<string, int>();
                coinPort = new Dictionary<string, string>();
                coinIP = new Dictionary<string, string>();
                coinUser = new Dictionary<string, string>();
                coinPassword = new Dictionary<string, string>();
                coinSigningAddress = new Dictionary<string, string>();
                coinTrackingAddress = new Dictionary<string, string>();
                coinGetRawSupport = new Dictionary<string, bool>();
                coinFeePerAddress = new Dictionary<string, bool>();
                coinEnableSigning = new Dictionary<string, bool>();
                coinEnableTracking = new Dictionary<string, bool>();
                coinHelperUrl = new Dictionary<string, string>();
                coinEnabled = new Dictionary<string, bool>();
                coinLastMemoryDump = new Dictionary<string, IEnumerable<string>>();
                string confLine;

                if (!System.IO.File.Exists("coin.conf"))
                {
                    System.IO.StreamWriter writeCoinConf = new StreamWriter("coin.conf");
                    writeCoinConf.WriteLine("Bitcoin 0 20 .0001 .00000548 330 8332 127.0.0.1 RPC_USER_CHANGE_ME RPC_PASSWORD_CHANGE_ME True False False False  BTC False  ");
                    writeCoinConf.WriteLine("Litecoin 48 20 .001 .00000001 330 9332 127.0.0.1 RPC_USER_CHANGE_ME RPC_PASSWORD_CHANGE_ME True True False False  LTC False  ");
                    writeCoinConf.WriteLine("Dogecoin 30 20 1 .00000001 330 22555 127.0.0.1 RPC_USER_CHANGE_ME RPC_PASSWORD_CHANGE_ME True True False False  DOGE False  ");
                    writeCoinConf.WriteLine("Mazacoin 50 20 .0001 .000055 330 12832 127.0.0.1 RPC_USER_CHANGE_ME RPC_PASSWORD_CHANGE_ME True True False False  MZC False  ");
                    writeCoinConf.WriteLine("Anoncoin 23 20 .01 .00000001 330 9376 127.0.0.1 RPC_USER_CHANGE_ME RPC_PASSWORD_CHANGE_ME True True False False  ANC False  ");
                    writeCoinConf.WriteLine("Devcoin 0 20 .6 .0000548 330 52332 127.0.0.1 RPC_USER_CHANGE_ME RPC_PASSWORD_CHANGE_ME True True False False  DVC False  ");
                    writeCoinConf.WriteLine("Potcoin 55 20 .001 .0000548 330 42000 127.0.0.1 RPC_USER_CHANGE_ME RPC_PASSWORD_CHANGE_ME True True False False  POT False  ");
             
                    writeCoinConf.Close();
                }


                if (!System.IO.Directory.Exists("root\\includes"))
                {
                    System.IO.Directory.CreateDirectory("root\\includes");

                    System.IO.StreamWriter writeIncludes = new StreamWriter("root\\includes\\meta.ssi");
                    writeIncludes.WriteLine("");
                    writeIncludes.Close();

                    writeIncludes = new StreamWriter("root\\includes\\header.ssi");
                    writeIncludes.WriteLine("");
                    writeIncludes.Close();

                    writeIncludes = new StreamWriter("root\\includes\\css.css");
                    writeIncludes.WriteLine("body {font-family: Arial, Helvetica, sans-serif;background:white;}");
                    writeIncludes.WriteLine("a {color: #A0A0A0;}");
                    writeIncludes.WriteLine("img {max-width: 100%;height: auto;}");
                    writeIncludes.WriteLine(".main{max-width: 100%;}");
                    writeIncludes.WriteLine(".main .item {margin: 5px;}");
                    writeIncludes.WriteLine(".main .item .content {word-wrap: break-word;background: #E6E6E6;color: #151515;padding: 10px;text-align: center;-webkit-border-radius:10px;-moz-border-radius:10px;border-radius:10px;}");
                    writeIncludes.WriteLine(".main .item .content table {word-break: break-all;word-wrap: break-word;padding: 10px;}");
                    writeIncludes.Close();

                    writeIncludes = new StreamWriter("root\\includes\\footer.ssi");
                    writeIncludes.WriteLine("");
                    writeIncludes.Close();
                }

                System.IO.StreamReader readCoinConf = new System.IO.StreamReader("coin.conf");
                while ((confLine = readCoinConf.ReadLine()) != null)
                {
                    string[] coins = new string[] { "Coin", "0", "20", ".00001", ".00000001", "330", "0000", "127.0.0.1", "RPC_USER_CHANGE_ME", "RPC_PASSWORD_CHANGE_ME", "True", "True", "False", "False", "", "", "False","", "" };
                    string[] loadCoin = confLine.Split(' ');
                    int intSetting = 0;
                    foreach (string s in loadCoin)
                    {
                        try
                        {
                            coins[intSetting] = s;
                            intSetting++;
                        }
                        catch { }
                    }

                    coinVersion.Add(coins[0], byte.Parse(coins[1]));
                    coinShortName.Add(coins[0], coins[15]);
                    coinPayloadByteSize.Add(coins[0], int.Parse(coins[2]));
                    coinTransactionFee.Add(coins[0], decimal.Parse(coins[3]));
                    coinMinTransaction.Add(coins[0], decimal.Parse(coins[4]));
                    // Transaction Sizes under 12 can cause an infinite loop that would drain a users wallet
                    if (int.Parse(coins[5]) > 11)
                    {
                        coinTransactionSize.Add(coins[0], int.Parse(coins[5]));
                    }
                    else { coinTransactionSize.Add(coins[0], 12); }
                    coinPort.Add(coins[0], coins[6]);
                    coinIP.Add(coins[0], coins[7]);
                    coinUser.Add(coins[0], coins[8]);
                    coinPassword.Add(coins[0], coins[9]);
                    if (coins[10].ToUpper() == "TRUE") { coinGetRawSupport.Add(coins[0], true); } else { coinGetRawSupport.Add(coins[0], false); }
                    if (coins[11].ToUpper() == "TRUE") { coinFeePerAddress.Add(coins[0], true); } else { coinFeePerAddress.Add(coins[0], false); }
                    if (coins[12].ToUpper() == "TRUE") { coinEnabled.Add(coins[0], true); } else { coinEnabled.Add(coins[0], false); }
                    if (coins[13].ToUpper() == "TRUE") { coinEnableSigning.Add(coins[0], true); } else { coinEnableSigning.Add(coins[0], false); }
                    coinSigningAddress.Add(coins[0], coins[14]);
                    coinTrackingAddress.Add(coins[0], coins[17]);
                    coinHelperUrl.Add(coins[0], coins[18]);
                    if (coins[16].ToUpper() == "TRUE") { coinEnableTracking.Add(coins[0], true); } else { coinEnableTracking.Add(coins[0], false); }
                    coinLastMemoryDump.Add(coins[0], null);

                }
                readCoinConf.Close();

                cmbCoinType.Items.Add("Blockchain");
                foreach (string item in coinVersion.Keys)
                {
                    if (coinEnabled[item])
                    {
                        cmbCoinType.Items.Add(item.ToString());
                    }
                }

                cmbCoinType.SelectedIndex = 0;
            }

            catch (Exception coinTypeException)
            {
                lblStatusInfo.Text = "Error: " + coinTypeException.Message + " check coin.conf file.";
                cmbCoinType.SelectedIndex = 0;
                cmbWalletLabel.SelectedIndex = 0;
                cmbWalletLabel.Enabled = false;
                tmrStatusUpdate.Start();
            }
        }

        private void updateEstimatedCost()
        {

            if (cmbCoinType.SelectedIndex != 0)
            {

                var estTotalCost = (decimal)0;
                var totalTransactionCost = (decimal)0;
                var totalFeeCost = (decimal)0;

                var totalSize = fileSize + txtMessage.TextLength;

                if (totalSize > 0)
                {
                    //does an ok job of estimating the archive cost
                    //
                    if (coinEnableSigning[cmbCoinType.Text]) { totalSize = totalSize + 160; }
                    //does an ok job of estimating the ledger size
                    if ( totalSize > (coinPayloadByteSize[cmbCoinType.Text] * coinTransactionSize[cmbCoinType.Text]))
                    {
                        totalSize = totalSize + (((Math.Round(totalSize / coinPayloadByteSize[cmbCoinType.Text], 0) / coinTransactionSize[cmbCoinType.Text]) * 64) + 70);
                    }
                    totalTransactionCost = Math.Round(totalSize / coinPayloadByteSize[cmbCoinType.Text], 0) * coinMinTransaction[cmbCoinType.Text];
                    if (coinFeePerAddress[cmbCoinType.Text]) { totalTransactionCost = totalTransactionCost + ((Math.Round(totalSize / coinPayloadByteSize[cmbCoinType.Text], 0)) * coinTransactionFee[cmbCoinType.Text]); }
                    totalFeeCost = Math.Round((Math.Round(totalSize / coinPayloadByteSize[cmbCoinType.Text], 0) / coinTransactionSize[cmbCoinType.Text]), 0) * coinTransactionFee[cmbCoinType.Text];
                    estTotalCost = totalTransactionCost + totalFeeCost + coinTransactionFee[cmbCoinType.Text] + coinMinTransaction[cmbCoinType.Text];
                    lblEstimatedCost.Text = estTotalCost.ToString();
                    lblTotalArchiveSize.Text = totalSize.ToString() + " bytes";
                }
                else
                {
                    lblEstimatedCost.Text = "0.00000000";
                }

            }


            else
            {
                lblEstimatedCost.Text = "0.00000000";
                lblCoinTotal.Text = "0.00000000";
            }
            CalculateLabelColors();

        }

        private void txtMessage_TextChanged(object sender, EventArgs e)
        {

            if (txtMessage.Text == "")
            {
                txtMessage.ScrollBars = ScrollBars.None;
                imgEnterMessageHere.Visible = true;
                if (txtFileName.TextLength < 1) { btnArchive.Enabled = false; }
            }
            else
            {
                txtMessage.ScrollBars = ScrollBars.Both;
                imgEnterMessageHere.Visible = false;

            }
            updateEstimatedCost();

        }

        private void cmbCoinType_SelectedIndexChanged(object sender, EventArgs e)
        {
            decimal totalValue = 0;

            if (fileSize > 0 || txtMessage.TextLength > 0)
            {
                updateEstimatedCost();
            }
            lblCoinTotal.Text = "0.00000000";
            cmbWalletLabel.Items.Clear();
            cmbWalletLabel.Items.Add("Account");
            cmbWalletLabel.SelectedIndex = 0;
            if (cmbCoinType.SelectedIndex > 0)
            {
                try
                {
                    CoinRPC a = new CoinRPC(new Uri(GetURL(coinIP[cmbCoinType.Text]) + ":" + coinPort[cmbCoinType.Text]), new NetworkCredential(coinUser[cmbCoinType.Text], coinPassword[cmbCoinType.Text]));
                    allAccounts = a.ListAccounts(1);
                    tmrStatusUpdate.Start();

                    try
                    {
                        foreach (string Account in allAccounts.Keys)
                        {
                            if (allAccounts[Account] > 0) { cmbWalletLabel.Items.Add(Account); }
                            totalValue = totalValue + allAccounts[Account];
                        }
                        lblCoinTotal.Text = totalValue.ToString();

                        var itemCountString = "";
                        if ((cmbWalletLabel.Items.Count - 1) > 1) { itemCountString = "s"; }
                        lblStatusInfo.Text = "Status: Blockchain connected " + (cmbWalletLabel.Items.Count - 1).ToString() + " suitable account" + itemCountString + " Found";

                        cmbWalletLabel.Enabled = true;
                    }
                    catch
                    {
                        lblStatusInfo.Text = "Status: No accounts found.  Check wallet settings.";
                        cmbCoinType.SelectedIndex = 0;
                        cmbWalletLabel.SelectedIndex = 0;
                        cmbWalletLabel.Enabled = false;
                        tmrStatusUpdate.Start();
                    }

                }
                catch (Exception walletException)
                {
                    lblStatusInfo.Text = "Error: " + walletException.Message + " check wallet settings.";
                    cmbCoinType.SelectedIndex = 0;
                    cmbWalletLabel.SelectedIndex = 0;
                    cmbWalletLabel.Enabled = false;
                    tmrStatusUpdate.Start();
                }

            }


        }

        private void txtFileName_TextChanged(object sender, EventArgs e)
        {
            if (txtFileName.Text == "")
            {
                fileSize = (decimal).00000000;
                if (txtMessage.TextLength < 1) { btnArchive.Enabled = false; }
                updateEstimatedCost();
            }
        }

        private void cmbWalletLabel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbWalletLabel.SelectedIndex > 0)
            {
                lblCoinTotal.Text = allAccounts[cmbWalletLabel.Text].ToString();
                txtMessage.Enabled = true;
                txtFileName.Enabled = true;
                btnAttachFile.Enabled = true;
                notarizeToolStripMenuItem.Enabled = true;
                txtMessage.Select();
                if (txtMessage.TextLength < 1) { imgEnterMessageHere.Visible = true; }

            }
            else
            {
                txtMessage.Enabled = false;
                txtFileName.Enabled = false;
                btnAttachFile.Enabled = false;
                notarizeToolStripMenuItem.Enabled = false;
                imgEnterMessageHere.Visible = false;
                btnArchive.Enabled = false;
                               

            }
            updateEstimatedCost();

        }

        private void CalculateLabelColors()
        {
            if (Decimal.Parse(lblEstimatedCost.Text) > Decimal.Parse(lblCoinTotal.Text))
            {
                lblEstimatedCost.ForeColor = System.Drawing.Color.Red;
                btnArchive.Enabled = false;
            }
            else
            {
                lblEstimatedCost.ForeColor = System.Drawing.Color.Black;
                if ((Decimal.Parse(lblEstimatedCost.Text) + (Decimal.Parse(lblEstimatedCost.Text) / 2)) < Decimal.Parse(lblCoinTotal.Text))
                { lblEstimatedCost.ForeColor = System.Drawing.Color.Green;
                if (cmbWalletLabel.SelectedIndex > 0) { btnArchive.Enabled = true; }
                }

            }
            if (txtFileName.TextLength == 0 && txtMessage.TextLength == 0) { btnArchive.Enabled = false; }
            if (lblEstimatedCost.ForeColor != System.Drawing.Color.Green && lblCoinTotal.Text != "0.00000000")
            {
                lblStatusInfo.Text = "Status: Deposit more coins to ensure a succesfull archive.";
                tmrStatusUpdate.Start();
            }
        }

        private string[] AddressArrayToLedger(string[] AddressArray, string WalletKey)
        {

            byte[] arcPayloadBytes = new byte[20];
            int intFileSize = 80;
            string strHeader = null;
            Boolean isLedgerFile = false;
            string[] headerArray = new String[2];
            byte[] fileArray = new byte[0];

            int f = 0;
            int h = 0;


            foreach (string line in AddressArray)
            {
                Base58.DecodeWithCheckSum(line, out arcPayloadBytes);
                for (int i = 1; i < arcPayloadBytes.Length; i++)
                {
                    if (f < intFileSize)
                    {

                        if (!isLedgerFile)
                        {
                            try
                            {
                                strHeader = strHeader + UTF8Encoding.UTF8.GetString(arcPayloadBytes, i, 1);
                            }
                            catch { return null; }

                            if (strHeader.IndexOfAny("\\/:*?\"><|".ToCharArray()) > -1)
                            {
                                headerArray[h] = strHeader.Remove(strHeader.Length - 1);
                                h++;
                                if (h > 1 && Int32.TryParse(headerArray[1], out intFileSize))
                                {

                                    try
                                    {
                                        var b = new CoinRPC(new Uri(GetURL(coinIP[WalletKey]) + ":" + coinPort[WalletKey]), new NetworkCredential(coinUser[WalletKey], coinPassword[WalletKey]));
                                        if (coinGetRawSupport[WalletKey])
                                        {
                                            var transaction = b.GetRawTransaction(headerArray[0], 1);
                                        }
                                        else
                                        {
                                            var transaction = b.GetTransaction(headerArray[0]);
                                        }
                                    }
                                    catch
                                    {
                                        return null;
                                    }
                                    isLedgerFile = true;
                                    fileArray = new byte[intFileSize];

                                }
                                strHeader = null;
                            }
                        }
                        else
                        {
                            fileArray[f] = arcPayloadBytes[i];
                            f++;
                        }
                    }

                }
            }

            if (isLedgerFile)
            {
                var LedgerResult = UTF8Encoding.UTF8.GetString(fileArray, 0, fileArray.Length);
                LedgerResult = LedgerResult.Replace("\n", "").TrimEnd((char)13);
                return LedgerResult.Split((char)13);
            }

            return null;

        }

        private Boolean ConvertAddressArrayToFile(string[] AddressArray, string TransID, string WalletKey, bool DisplayResults)
        {
            try
            {
                int intFileByteCount = 0;
                int intPayLoadSize = 0;
                byte[] rawBytes = null;


                // Converting Addresses to RawBytes
                foreach (string address in AddressArray)
                {
                    byte[] arcPayloadBytes = null;
                    Base58.DecodeWithCheckSum(address, out arcPayloadBytes);

                    //Supporting different Payload Byte Sizes
                    if (rawBytes == null)
                    {
                        intPayLoadSize = arcPayloadBytes.Count() - 1;
                        rawBytes = new byte[(AddressArray.Count() * intPayLoadSize)];
                    }
                    //Building rawbytes
                    ArrayHelpers.SubArray(arcPayloadBytes, 1).CopyTo(rawBytes, intFileByteCount);
                    intFileByteCount = intFileByteCount + intPayLoadSize;
                }

                string strHeaderBuilder = "";
                string[] header = new string[2];
                int intHeaderPosition = 0;
                int intFilePosition = 0;
                int intFileSize = 0;
                string strFileName = "";
                byte[] buildingFile = null;
                Boolean containsData = false;
                int intStartSignedPosition = 0;
                int intEndSignedPosition = 0;
                string strSig = null;
                string strSigAddress = null;
                Boolean isSigned = false;
                Boolean trustContent = false;
                var buildFiles = new Dictionary<string, byte[]>();


                for (int i = 0; i < rawBytes.Length; i++)
                {
                    if (buildingFile == null)
                    {
                        if (UTF8Encoding.UTF8.GetString(rawBytes, i, 1).IndexOfAny("\\/:*?\"><|".ToCharArray()) > -1)
                        {
                            header[intHeaderPosition] = strHeaderBuilder;
                            strHeaderBuilder = "";
                            intHeaderPosition++;
                        }
                        else { strHeaderBuilder = strHeaderBuilder + UTF8Encoding.UTF8.GetString(rawBytes, i, 1); }

                        //Has Found enough Special Characters for a simple file Check
                        if (intHeaderPosition == 2)
                        {
                            var containsFileSize = int.TryParse(header[1], out intFileSize);
                            if (!containsFileSize || ((rawBytes.Length - i) < intFileSize) || header[0].Count() > 255 || header[0].IndexOfAny(Path.GetInvalidFileNameChars()) > -1 || (header[0].Count() == 0 && intFileSize == 0)) { return false; }
                            intHeaderPosition = 0;
                            intFilePosition = 0;
                            strFileName = header[0];
                            buildingFile = new byte[intFileSize];
                            containsData = true;
                            transactionsFound++;
                            intEndSignedPosition = i + intFileSize;

                        }
                    }
                    else
                    {

                        if (intFilePosition < intFileSize)
                        {
                            buildingFile[intFilePosition] = rawBytes[i];
                            intFilePosition++;
                            //intEndSignedPosition = i;
                        }
                        else
                        {
                            //checking for valid unicode characters to filter out noise
                            if (header[0] == "" && buildingFile.Length < 21)
                            {
                                for (int c = 0; c < buildingFile.Length; c++)
                                {
                                    if (!characterMap.ContainsKey(buildingFile[c])) { return false; };
                                }
                            }

                            buildFiles.Add(header[0], buildingFile);

                            i = i - 1;

                            if (Path.GetExtension(header[0]).ToUpper() == ".SIG" || header[0] == "SIG")
                            {
                                intStartSignedPosition = i;

                                if (header[0] == "SIG")
                                { strSigAddress = AddressArray[AddressArray.Count() - 1]; }
                                else { strSigAddress = Path.GetFileNameWithoutExtension(header[0]); }
                                strSig = Encoding.UTF8.GetString(buildingFile, 0, buildingFile.Length);

                            }
                            buildingFile = null;

                        }

                    }

                }
                //good enough for now
                if (intFilePosition == intFileSize && buildingFile != null)
                {
                    //checking for valid unicode characters to filter out noise
                    if (header[0] == "" && buildingFile.Length < 21)
                    {
                        for (int c = 0; c < buildingFile.Length; c++)
                        {
                            if (!characterMap.ContainsKey(buildingFile[c])) { return false; };
                        }
                    }
                    buildFiles.Add(header[0], buildingFile);
                    
                    
                }

                if (intStartSignedPosition > 0)
                {
                    System.Security.Cryptography.SHA256 mySHA256 = SHA256Managed.Create();
                    byte[] hashValue = mySHA256.ComputeHash(rawBytes.Skip(intStartSignedPosition + 1).Take(intEndSignedPosition - intStartSignedPosition).ToArray());
                    var a = new CoinRPC(new Uri(GetURL(coinIP[WalletKey]) + ":" + coinPort[WalletKey]), new NetworkCredential(coinUser[WalletKey], coinPassword[WalletKey]));
                    isSigned = a.VerifyMessage(strSigAddress, strSig, BitConverter.ToString(hashValue).Replace("-", String.Empty));

                }
                if (!isSigned && blockUnSignedContent)
                {
                    return false;
                }

                if (blockUntrustedContent && (!isSigned || !hashTrustedList.Contains(strSigAddress)))
                {
                    return false;
                }

                if (blockBlockedListContent && hashBlockedList.Contains(strSigAddress))
                {
                    return false;
                }

                if (trustTrustedlistContent && isSigned && hashTrustedList.Contains(strSigAddress))
                {
                    trustContent = true;
                }

                if (containsData)
                {
                    Directory.CreateDirectory("root\\" + TransID);


                    FileStream fileStream = new FileStream("root\\" + TransID + "\\index.htm", FileMode.Create);
                    fileStream.Write(UTF8Encoding.UTF8.GetBytes("<html><head><meta charset=\"UTF-8\" /><title>" + WalletKey + " - " + TransID + "</title><meta name=\"description\" content=\"The following content was archived to the " + WalletKey + " blockchain.\" /><meta name=viewport content=\"width=device-width, initial-scale=1\"><!--#include file=\"..\\includes\\meta.ssi\" --><link rel=\"stylesheet\" type=\"text/css\" href=\"..\\includes\\css.css\"></head><body><div class=\"main\"><!--#include file=\"..\\includes\\header.ssi\" -->"), 0, 399 + (WalletKey.Length * 2) + TransID.Length);
                    fileStream.Close();

                    foreach (KeyValuePair<string, byte[]> entry in buildFiles)
                    {
                        ParseData(entry.Value, TransID, entry.Key, trustContent, chkMonitorBlockChains.Checked);
                    }

                    fileStream = new FileStream("root\\" + TransID + "\\index.htm", FileMode.Append);

                    string printDate = "";
                    if (coinGetRawSupport[WalletKey])
                    {
                        var b = new CoinRPC(new Uri(GetURL(coinIP[WalletKey]) + ":" + coinPort[WalletKey]), new NetworkCredential(coinUser[WalletKey], coinPassword[WalletKey]));
                        var transaction = b.GetRawTransaction(TransID, 1);
                        printDate = "PENDING";
                        //place in batch queue to be tried again later.
                        if (transaction.blocktime == 0)
                        {
                            lock (_batchLocker)
                            {
                                if (!System.IO.File.Exists("batch.txt"))
                                {
                                    System.IO.File.AppendAllText("batch.txt", TransID + "@" + WalletKey + Environment.NewLine);
                                }
                                else
                                {
                                    if (!System.IO.File.ReadAllText("batch.txt").Contains(TransID))
                                    {
                                        System.IO.File.AppendAllText("batch.txt", TransID + "@" + WalletKey + Environment.NewLine);
                                    }
                                }
                            }
                        }
                        else
                        {
                            System.DateTime dateTime = new System.DateTime(1970, 1, 1, 0, 0, 0, 0);
                            dateTime = dateTime.AddSeconds(transaction.blocktime);
                            printDate = dateTime.ToShortDateString() + " " + dateTime.ToShortTimeString();
                        }

                        fileStream.Write(UTF8Encoding.UTF8.GetBytes("<div class=\"item\"><div class=\"content\"><table>"), 0, 46);
                        if (coinHelperUrl[WalletKey] != "")
                        { fileStream.Write(UTF8Encoding.UTF8.GetBytes("<tr><td>ROOT ID</td><td><a href=\"" + coinHelperUrl[WalletKey].Replace("%s", transaction.txid) + "\">" + transaction.txid + "</a></td></tr>"), 0, 49 + transaction.txid.Length + coinHelperUrl[WalletKey].Replace("%s", transaction.txid).Length); }
                        else
                        { fileStream.Write(UTF8Encoding.UTF8.GetBytes("<tr><td>ROOT ID</td><td>" + transaction.txid + "</td></tr>"), 0, 34 + transaction.txid.Length); }
                        if (isSigned)
                        {
                            fileStream.Write(UTF8Encoding.UTF8.GetBytes("<tr><td>SIGNED BY</td><td><a href=\"SIG\">" + strSigAddress + "</a></td></tr>"), 0, 54 + (strSigAddress.Length));
                        }
                        fileStream.Write(UTF8Encoding.UTF8.GetBytes("<tr><td>BLOCK DATE</td><td nowrap>" + printDate + "</td></tr>"), 0, 44 + printDate.Length);
                        fileStream.Write(UTF8Encoding.UTF8.GetBytes("<tr><td>VERSION</td><td>" + transaction.version + "</td></tr>"), 0, 34 + transaction.version.ToString().Length);
                        fileStream.Write(UTF8Encoding.UTF8.GetBytes("<tr><td>BLOCKCHAIN</td><td>" + WalletKey + "</td></tr>"), 0, 37 + WalletKey.Length);
                        fileStream.Write(UTF8Encoding.UTF8.GetBytes("<tr><td nowrap>ADDRESS FILE</td><td><a href=\"ADD\">Address.dat</a></td></tr>"), 0, 67);
                        if (Properties.Settings.Default.ReportAbuseUrl != "")
                        { fileStream.Write(UTF8Encoding.UTF8.GetBytes("<tr><td colspan=2 align=right><a href=\"" + Properties.Settings.Default.ReportAbuseUrl.Replace("%s", transaction.txid) + "\">report abuse</a></td></tr>"), 0, 67 + Properties.Settings.Default.ReportAbuseUrl.Replace("%s", transaction.txid).Length); }
                        fileStream.Write(UTF8Encoding.UTF8.GetBytes("</table></div></div>"), 0, 20);
                    }

                    FileStream dataFileStream = new FileStream("root\\" + TransID + "\\ADD", FileMode.Create);
                    foreach (string address in AddressArray)
                    {
                        dataFileStream.Write(UTF8Encoding.UTF8.GetBytes(address + "\n"), 0, address.Length + 1);
                    }
                    dataFileStream.Close();

                    fileStream.Write(UTF8Encoding.UTF8.GetBytes("<!--#include file=\"..\\includes\\footer.ssi\" --></div></body></html>"), 0, 66);
                    fileStream.Close();
                    
                    //Don't crawl raw monitor results
                    if (DisplayResults && !chkMonitorBlockChains.Checked)
                    {
                        BuildBackLinks(TransID);
                        Uri BrowseURL = new Uri(System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "/root/" + TransID + "/index.htm");
                        webBrowser1.Url = BrowseURL;
                        
                    }

                    if (printDate != "PENDING")
                    {
                        if (!File.Exists("root\\catalog.htm")) {System.IO.File.AppendAllText("root\\catalog.htm","");}
                        var strFileDump = System.IO.File.ReadAllText("root\\catalog.htm");
                        if (!strFileDump.Contains(TransID))
                        {
                            System.IO.File.AppendAllText("root\\catalog.htm", "<a href=\"" + Properties.Settings.Default.HistoryTransactionIDUrl.Replace("%s", TransID) + "\">" + TransID + "</a><br>" + Environment.NewLine);
                            if (Properties.Settings.Default.SiteMapTransactionIdUrl != "")
                            { System.IO.File.AppendAllText("root\\sitemap.txt", Properties.Settings.Default.SiteMapTransactionIdUrl.Replace("%s", TransID) + Environment.NewLine); }
                        }
                        
                    }
                }
                return containsData;
            }
            catch { return false; }
        }

        private void BuildBackLinks(string TransID)
        {
            foreach (var files in Directory.GetFiles("root\\" + TransID))
            {
                FileInfo info = new FileInfo(files);
                var fileName = Path.GetFileName(info.FullName);

                string fileText = string.Empty;
                using (StreamReader streamReader = new StreamReader("root\\" + TransID + "\\" + fileName, Encoding.UTF8))
                {
                    fileText = streamReader.ReadToEnd();
                }
                Match match = Regex.Match(fileText, @"\.\.\/([a-fA-F0-9]{64})");
                if (match.Success)
                {
                    do
                    {
                        var isFound = false;
                        foreach (string i in cmbCoinType.Items)
                        {
                            if (i != "Blockchain" && isFound == false)
                            {
                                lock (_buildLocker)
                                {
                                    isFound = CreateArchive(match.ToString().Replace("../", ""), i, false, true);
                                }

                                if (isFound)
                                {
                                    break;
                                }
                            }
                        }

                        match = match.NextMatch();
                    } while (match.Success);
                }
            }
        }

        private void ParseData(byte[] ByteData, string TransID, string FileName, bool TrustContent, bool SendToMonitor)
        {

            Boolean foundType = false;
            string strPrintLine = "";

            if (FileName != "")
            {
                HashSet<string> safeExtensions =
               new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".gif", ".jpg", ".jpeg", ".txt", ".tiff", ".tif", ".mp3", ".mpeg", ".mpg", ".wav"
                };

                HashSet<string> embExtensions =
              new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".m2ts", ".aac", ".adt", ".adts", ".3g2", ".3gp2", ".3gp", ".3gpp", ".m4a", ".mov", ".wmz", ".wms", ".ivf", ".cda", ".wav", ".au", ".snd", ".aif", ".aifc", ".aiff", ".mid", ".midi", ".rmi", ".mpg", ".mpeg", ".m1v", ".mp2", ".mp3", ".mpa", ".mpe", ".m3u", ".avi", ".wmd", ".dvr-ms", ".wpi", ".wax", ".wvx", ".wmx", ".asf", ".wma", ".wmv", ".wm", ".swf", ".pdf"
                };

                HashSet<string> imgExtensions =
               new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg", ".jpeg", ".jpe", ".gif", ".png", ".tiff", ".tif", ".svg", ".svgz", ".xbm", ".bmp", ".ico"
                };

                if (chkFilterUnSafeContent.Checked && !TrustContent && !safeExtensions.Contains(Path.GetExtension(FileName)))
                {

                    FileStream fileStream = new FileStream("root\\" + TransID + "\\index.htm", FileMode.Append);
                    strPrintLine = "<div class=\"item\"><div class=\"content\">[ " + FileName + " ]</div></div>";
                    fileStream.Write(UTF8Encoding.UTF8.GetBytes(strPrintLine), 0, UTF8Encoding.UTF8.GetBytes(strPrintLine).Length);
                    fileStream.Close();
                    foundType = true;
                }
                else
                {
                    TrustContent = true;
                }


                if (!foundType && embExtensions.Contains(Path.GetExtension(FileName)))
                {
                    FileStream fileStream = new FileStream("root\\" + TransID + "\\index.htm", FileMode.Append);
                    strPrintLine = "<div class=\"item\"><div class=\"content\"><embed src=\"" + HttpUtility.UrlPathEncode(FileName) + "\" /><p><a href=\"" + HttpUtility.UrlPathEncode(FileName) + "\">" + FileName + "</a></p></div></div>";
                    fileStream.Write(UTF8Encoding.UTF8.GetBytes(strPrintLine), 0, UTF8Encoding.UTF8.GetBytes(strPrintLine).Length);
                    fileStream.Close();
                    foundType = true;
                }

                if (!foundType && imgExtensions.Contains(Path.GetExtension(FileName)))
                {
                    FileStream fileStream = new FileStream("root\\" + TransID + "\\index.htm", FileMode.Append);
                    strPrintLine = "<div class=\"item\"><div class=\"content\"><img src=\"" + HttpUtility.UrlPathEncode(FileName) + "\" /><br><a href=\"" + HttpUtility.UrlPathEncode(FileName) + "\">" + FileName + "</a></div></div>";
                    fileStream.Write(UTF8Encoding.UTF8.GetBytes(strPrintLine), 0, UTF8Encoding.UTF8.GetBytes(strPrintLine).Length);
                    fileStream.Close();
                    foundType = true;
                }

                if (!foundType)
                {
                    if (Path.GetExtension(FileName).ToUpper() != ".SIG" && FileName != "SIG")
                    {
                        FileStream fileStream = new FileStream("root\\" + TransID + "\\index.htm", FileMode.Append);
                        strPrintLine = "<div class=\"item\"><div class=\"content\"><a href=\"" + HttpUtility.UrlPathEncode(FileName) + "\">" + HttpUtility.UrlPathEncode(FileName) + "</a></div></div>";
                        fileStream.Write(UTF8Encoding.UTF8.GetBytes(strPrintLine), 0, UTF8Encoding.UTF8.GetBytes(strPrintLine).Length);
                        fileStream.Close();
                    }
                }

              

            if (TrustContent)
                {
                    FileStream attachStream = new FileStream("root\\" + TransID + "\\" + FileName, FileMode.Create);
                    attachStream.Write(ByteData, 0, ByteData.Length);
                    attachStream.Close();
                }


  

            }
            else
            {
                string result = System.Text.UTF8Encoding.UTF8.GetString(ByteData);
                if (chkFilterUnSafeContent.Checked && !TrustContent)
                {
                    //Limited Protection From Injection Hacks
                    result = WebUtility.HtmlEncode(result);
                }

                msgId++;
                FileStream fileStream = new FileStream("root\\" + TransID + "\\MSG" + msgId , FileMode.Create);
                fileStream.Write(UTF8Encoding.UTF8.GetBytes(result), 0, UTF8Encoding.UTF8.GetBytes(result).Length);
                fileStream.Close();

                fileStream = new FileStream("root\\" + TransID + "\\index.htm", FileMode.Append);
                strPrintLine = "<div class=\"item\"><div class=\"content\">" + result + "<p><a href=\"MSG" + msgId + "\">MSG" + msgId + "</a></p></div></div>";
                fileStream.Write(UTF8Encoding.UTF8.GetBytes(strPrintLine), 0, UTF8Encoding.UTF8.GetBytes(strPrintLine).Length);
                fileStream.Close();

                                  
            }

            if (SendToMonitor)
            {
                string line = "";

                if (!System.IO.File.Exists("monitor.htm"))
                {
                    StreamWriter newFile = new StreamWriter("monitor.htm");
                    newFile.WriteLine("<html><head><meta charset=\"UTF-8\" /></head><link rel=\"stylesheet\" type=\"text/css\" href=\"" + System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "\\root\\includes\\css.css\"><body><div class=\"main\">");
                    newFile.WriteLine("</div></body></html>");
                    newFile.Close();
                }
                StreamWriter tmpFile = new StreamWriter("~Monitor.htm");
                StreamReader readFile = new StreamReader("monitor.htm");
                tmpFile.WriteLine(readFile.ReadLine());
                strPrintLine = strPrintLine.Replace("href=\"", "href=\"" + System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "\\root\\" + TransID + "\\");
                strPrintLine = strPrintLine.Replace("src=\"", "src=\"" + System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "\\root\\" + TransID + "\\");
                strPrintLine = strPrintLine.Replace("%20", " ");
       
                tmpFile.WriteLine("<A href=\""+ System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "\\root\\" + TransID + "\\index.htm" + "\">" + strPrintLine + "</a>");
                while ((line = readFile.ReadLine()) != null)
                {
                    tmpFile.WriteLine(line);                    
                }
                tmpFile.Close();
                readFile.Close();
                File.Delete("monitor.htm");
                File.Move("~Monitor.htm", "monitor.htm");

                Uri MonitorUrl = new Uri(System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "\\monitor.htm");
                webBrowser1.Url = MonitorUrl;
                
            }
     

        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            lblStatusInfo.Width = Main.ActiveForm.Width - 160;
            
        }

        private void tmrStatusUpdate_Tick(object sender, EventArgs e)
        {
            lblStatusInfo.Text = "Status:";
            tmrStatusUpdate.Stop();
        }

        private void tmrProgressBar_Tick(object sender, EventArgs e)
        {
            progressBar.Visible = false;
            tmrProgressBar.Stop();
        }

        public Boolean CreateArchive(string TransactionID, string WalletKey, bool DisplayResults, bool UseCache)
        {

            string[] AddressArray = null;
            string[] LedgerArray = null;
            msgId = 0;
            RefreshTrustCache();

            if (UseCache && System.IO.Directory.Exists("root\\" + TransactionID))
            {
                Uri BrowseURL = new Uri(System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "/root/" + TransactionID + "/index.htm");
                webBrowser1.Url = BrowseURL;
                return true;
            }

            try
            {
                AddressArray = CreateAddressArrayFromTransactionID(TransactionID, WalletKey);
            }
            catch { return false; }

            try
            {
                do
                {

                    LedgerArray = AddressArrayToLedger(AddressArray, WalletKey);
                    var delimiter = "";
                    var LedgerDelimited = "";
                    if (LedgerArray != null)
                    {
                        foreach (string line in LedgerArray)
                        {

                            AddressArray = CreateAddressArrayFromTransactionID(line, WalletKey);
                            LedgerDelimited = LedgerDelimited + delimiter + string.Join(",", AddressArray);
                            delimiter = ",";
                        }
                        AddressArray = LedgerDelimited.Split(',');
                    }

                } while (LedgerArray != null);

                return ConvertAddressArrayToFile(AddressArray, TransactionID, WalletKey, DisplayResults);
            }
            catch { return false; }

        }

        private string[] CreateAddressArrayFromTransactionID(string TransactionId, string WalletKey)
        {
            var delimiter = "";
            string addressBuilder = "";
            var arcValue = (decimal)999999999;

            var b = new CoinRPC(new Uri(GetURL(coinIP[WalletKey]) + ":" + coinPort[WalletKey]), new NetworkCredential(coinUser[WalletKey], coinPassword[WalletKey]));

            if (coinGetRawSupport[WalletKey])
            {

                var transaction = b.GetRawTransaction(TransactionId, 1);


                foreach (BitcoinNET.RPCClient.GetRawTransactionResponse.Output detail in transaction.vout)
                {
                    if (arcValue > detail.value)
                    {
                        arcValue = detail.value;
                    }
                }

                foreach (BitcoinNET.RPCClient.GetRawTransactionResponse.Output detail in transaction.vout)
                {

                    if (detail.value == arcValue)
                    {
                        addressBuilder = addressBuilder + delimiter + detail.scriptPubKey.addresses[0];
                        delimiter = ",";
                    }
                }

            }
            else
            {
                var transaction = b.GetTransaction(TransactionId);

                foreach (BitcoinNET.RPCClient.GetTransactionResponse.Details detail in transaction.details)
                {
                    if (arcValue > detail.amount && detail.amount > 0)
                    {
                        arcValue = detail.amount;
                    }
                }

                foreach (BitcoinNET.RPCClient.GetTransactionResponse.Details detail in transaction.details)
                {
                    if (detail.amount == arcValue && detail.category == "receive")
                    {
                        addressBuilder = addressBuilder + delimiter + detail.address;
                        delimiter = ",";
                    }
                }

            }

            return addressBuilder.Split(',');

        }

        private void chkMonitorBlockChains_CheckedChanged(object sender, EventArgs e)
        {
            var coinCount = 0;
            foreach (var i in coinIP)
            {
                if (coinGetRawSupport[i.Key] && coinEnabled[i.Key])
                {
                    try
                    {
                        var b = new CoinRPC(new Uri(GetURL(coinIP[i.Key]) + ":" + coinPort[i.Key]), new NetworkCredential(coinUser[i.Key], coinPassword[i.Key]));
                        coinLastMemoryDump[i.Key] = b.GetRawMemPool();
                    }
                    catch (Exception ex)
                    {
                       lblStatusInfo.Text = "Error: " + i.Key + " " + ex.Message;
                        tmrStatusUpdate.Start();
                    }
                    coinCount++;
                }


            }

            if (coinCount == 0)
            {
                lblStatusInfo.Text = "Status: Nothing to Monitor.  Check Wallet Settings";
                tmrStatusUpdate.Start();
                chkMonitorBlockChains.Checked = false;
            }




            if (chkMonitorBlockChains.Checked)
            {
                tmrGetNewTransactions.Start();
                lblMonitorInfo.Visible = true;
            }
            else
            {
                tmrGetNewTransactions.Stop();
                lblMonitorInfo.Visible = false;
            }
        }

        private void tmrGetNewTransactions_Tick(object sender, EventArgs e)
        {
            IEnumerable<string> transaction = null;
            try
            {
                foreach (var i in coinIP)
                {
                    if (coinEnabled[i.Key] && coinGetRawSupport[i.Key])
                    {
                        try
                        {
                            var b = new CoinRPC(new Uri(GetURL(coinIP[i.Key]) + ":" + coinPort[i.Key]), new NetworkCredential(coinUser[i.Key], coinPassword[i.Key]));
                            transaction = b.GetRawMemPool();

                            IEnumerable<string> differenceQuery =
                            transaction.Except(coinLastMemoryDump[i.Key]);

                            foreach (var s in differenceQuery)
                            {
                                lock (_buildLocker)
                                {
                                    CreateArchive(s, i.Key, true, false);
                                }

                                transactionsSearched++;
                            }

                            coinLastMemoryDump[i.Key] = transaction;
                            lblMonitorInfo.Text = "Monitor: " + transactionsSearched.ToString() + " / " + transactionsFound.ToString();

                        }
                        catch (Exception ex)
                        {
                            lblStatusInfo.Text = "Error: " + i.Key + " Check Wallet Settings. " + ex.Message;
                            if (coinLastMemoryDump[i.Key] == null) { coinLastMemoryDump[i.Key] = transaction; }
                            tmrStatusUpdate.Start();
                        }
                    }
                }
            }
            catch
            {
                tmrGetNewTransactions.Stop();
                chkMonitorBlockChains.Checked = false;
            }

        }

        private void fontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FontDialog fontDialog1 = new FontDialog();
            fontDialog1.Font = txtMessage.Font;
            fontDialog1.Color = txtMessage.ForeColor;

            if (fontDialog1.ShowDialog() != DialogResult.Cancel)
            {
                txtMessage.Font = fontDialog1.Font;
                txtMessage.ForeColor = fontDialog1.Color;
            }
        }

        private void txtTransIDSearch_KeyDown(object sender, KeyEventArgs e)
        {
            
            if (e.KeyValue == 13)
            {
                performSearch(ModifierKeys != Keys.Control);                
            }

            if (e.KeyValue == (int)Keys.Delete)
            {

                txtTransIDSearch.Text = " ENTER SEARCH STRING";
                txtTransIDSearch.ForeColor = System.Drawing.Color.LightGray;
            }

             if (e.KeyValue == (int)Keys.Back)
            {

                if (txtTransIDSearch.Text == "ENTER SEARCH STRING")
                { txtTransIDSearch.Text = "ENTER SEARCH STRING";
                txtTransIDSearch.ForeColor = System.Drawing.Color.LightGray;
                }
            }
             
        }

        private void performSearch(bool useCache)
        {
            var isFound = false;


             foreach (string i in cmbCoinType.Items)
            {
                if (i != "Blockchain" && isFound == false)
                {
                    lock (_buildLocker)
                    {
                        isFound = CreateArchive(txtTransIDSearch.Text, i, true, useCache);
                    }

                    if (isFound)
                    {
                        lblStatusInfo.Text = "Status: Found in Blockchain";
                        tmrStatusUpdate.Start();
                        break;
                    }
                }
            }
            if (!isFound)
            {
                lblStatusInfo.Text = "Status: Transaction Id not found in linked Bockchains.";
                tmrStatusUpdate.Start();
            }
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("ADD.EXE");
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void walletsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(OpenWalletForm));
            t.Start();
        }

        public static void OpenWalletForm()
        {
            Application.Run(new Wallets());
        }

        public static void OpenAboutForm()
        {
            Application.Run(new About());
        }

        public static void OpenTrustForm()
        {
            Application.Run(new Trust());
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(OpenAboutForm));
            t.Start();

        }

        private void txtTransIDSearch_Click(object sender, EventArgs e)
        {
            if (txtTransIDSearch.Text == "ENTER SEARCH STRING") {
                txtTransIDSearch.Text = "";
                txtTransIDSearch.ForeColor = System.Drawing.Color.Black;
            }

        }

        //private void searchResults_LinkClicked(object sender, LinkClickedEventArgs e)
        //{
        //    try
        //    {
        //        System.Diagnostics.Process.Start("root\\" + e.LinkText.Substring(6).Replace("/", "\\"));
        //    }
        //    catch (Exception ex)
        //    {
        //        lblStatusInfo.Text = "Error: " + ex.Message;
        //        tmrStatusUpdate.Start();
        //    }
        //}

        private void notarizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = openDigestFile.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                FileStream filestream;
                System.Security.Cryptography.SHA256 mySHA256 = SHA256Managed.Create();
                filestream = new FileStream(openDigestFile.FileName, FileMode.Open);
                filestream.Position = 0;
                byte[] hashValue = mySHA256.ComputeHash(filestream);
                filestream.Close();
                string strHash = BitConverter.ToString(hashValue).Replace("-", String.Empty);
                byte[] payLoadBytes = new byte[21];
                payLoadBytes[0] = coinVersion[cmbCoinType.Text];
                byte[] hashBytes = Encoding.UTF8.GetBytes(strHash.Substring(0,20));
                hashBytes.CopyTo(payLoadBytes,1);
                string strProofAddress = Base58.EncodeWithCheckSum(payLoadBytes);
           
                CoinRPC a = new CoinRPC(new Uri(GetURL(coinIP[cmbCoinType.Text]) + ":" + coinPort[cmbCoinType.Text]), new NetworkCredential(coinUser[cmbCoinType.Text], coinPassword[cmbCoinType.Text]));
                try
                {
                    a.ImportAddress(strProofAddress, openDigestFile.FileName, false);
                }
                catch
                {

                    DialogResult dialogResult = MessageBox.Show("This blockchain does not support file proof Lookups at this time. Consider setting up a tracking address in the wallet settings BEFORE inserting the proof. Do you wish to continue?", "Limited functions", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.No)
                    {
                        return;
                    }
                }
                
                CreateLedgerFile(coinPayloadByteSize[cmbCoinType.Text], GetRandomBuffer(coinPayloadByteSize[cmbCoinType.Text]), coinIP[cmbCoinType.Text], coinPort[cmbCoinType.Text], coinUser[cmbCoinType.Text], coinPassword[cmbCoinType.Text], cmbWalletLabel.Text, coinVersion[cmbCoinType.Text], coinMinTransaction[cmbCoinType.Text], "", null, strHash +"\n" + "FileName:" + openDigestFile.FileName);

            }

        }

        private void imgEnterMessageHere_Click(object sender, EventArgs e)
        {
            txtMessage.Select();
        }

        private void rebuildToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Threading.Thread t = new System.Threading.Thread(RebuildRootIndex);
            t.Start();
        }

        private void RebuildRootIndex()
        {
            string[] folders = Directory.GetDirectories("root");

            foreach (string f in folders)
            {
                bool isFound = false;

                foreach (string i in cmbCoinType.Items)
                {
                    if (i != "Blockchain" && isFound == false)
                    {
                        string transID = f.Replace("root\\", "");
                        lock (_buildLocker)
                        {
                            isFound = CreateArchive(transID, i, true, false);
                        }
                        if (isFound)
                        {
                            lblStatusInfo.Text = "Status: Found in Blockchain";
                            tmrStatusUpdate.Start();
                            break;
                        }
                    }
                }
                if (!isFound)
                {
                    lblStatusInfo.Text = "Status: Transaction Id not found in linked Bockchains.";
                    tmrStatusUpdate.Start();
                }

            }
        }

        private void trustToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(OpenTrustForm));
            t.Start();

        }

        private void tmrProcessBatch_Tick(object sender, EventArgs e)
        {
            var batchLine = "";
            List<string> batchList = new List<string>();

            lock (_batchLocker)
            {
                try
                {
                    System.IO.StreamReader readBatch = new System.IO.StreamReader("batch.txt");
                    while ((batchLine = readBatch.ReadLine()) != null)
                    {
                        batchList.Add(batchLine);
                    }
                    readBatch.Close();
                    System.IO.File.Delete("batch.txt");
                }
                catch { }
            }


            foreach (string transIDKey in batchList)
            {
                var transArray = transIDKey.Split('@');
                try
                {
                    lock (_buildLocker)
                    {
                        CreateArchive(transArray[0], transArray[1], false,false);
                    }
                }
                catch { }
            }
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        private void tmrUpdateInfoText_Tick(object sender, EventArgs e)
        {
            Random random = new Random();
            txtInfoBox.Text = infoArray[random.Next(0, infoArray.Count())];
        }

        private void webBrowser1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                e.IsInputKey = false;
                return;
            }
        }

        private void chkFilterUnSafeContent_CheckedChanged(object sender, EventArgs e)
        {
            if (!chkFilterUnSafeContent.Checked)
            {
                if (chkMonitorBlockChains.Checked)
                {
                    chkMonitorBlockChains.Checked = false;
                    DialogResult dialogResult = MessageBox.Show("NOTICE: Monitoring with the filter disabled is crazy dangerous. Do you want to continue?", "Confirm Saving", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.Yes)
                    {
                        chkMonitorBlockChains.Checked = true;
                    }
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {


            CoinRPC a = new CoinRPC(new Uri(GetURL(coinIP[cmbCoinType.Text]) + ":" + coinPort[cmbCoinType.Text]), new NetworkCredential(coinUser[cmbCoinType.Text], coinPassword[cmbCoinType.Text]));

            var privKeyHex = BitConverter.ToString(Base58.Decode(a.DumpPrivateKey("PECZDPtEhCF9jP1kzNJJxW8hq9RZ79xeDE"))).Replace("-", "");
            privKeyHex = privKeyHex.Substring(2, 64);
            BigInteger privateKey = Hex.HexToBigInteger(privKeyHex);
            ECPoint publicKey = Secp256k1.Secp256k1.G.Multiply(privateKey);
            //string bitcoinAddressUncompressed = publicKey.GetBitcoinAddress(false);
            //string bitcoinAddressCompressed = publicKey.GetBitcoinAddress(compressed: true);
            ECEncryption encryption = new ECEncryption();
            var readFileBytes = System.IO.File.ReadAllBytes(txtFileName.Text);
            byte[] encrypted = encryption.Encrypt(publicKey, readFileBytes);
            System.IO.File.WriteAllBytes("CNG", encrypted);
            byte[] decrypted = encryption.Decrypt(privateKey, encrypted);
            System.IO.File.WriteAllBytes(txtFileName.Text + ".new", decrypted);
            
        }

 

        //private void backgroundWorker1_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        //{
        //    this.btnArchive.Enabled = true;
        //    this.txtFileName.Enabled = true;
        //    this.txtMessage.Enabled = true;
        //    this.chkKeywords.Enabled = true;
        //    //this.chkPrivate.Enabled = true;
        //    this.chkRecipients.Enabled = true;

        //}

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {

            DialogResult result = openDigestFile.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                FileStream filestream;
                System.Security.Cryptography.SHA256 mySHA256 = SHA256Managed.Create();
                filestream = new FileStream(openDigestFile.FileName, FileMode.Open);
                filestream.Position = 0;
                byte[] hashValue = mySHA256.ComputeHash(filestream);
                filestream.Close();
                string strHash = BitConverter.ToString(hashValue).Replace("-", String.Empty);
                byte[] payLoadBytes = new byte[21];
                payLoadBytes[0] = coinVersion[cmbCoinType.Text];
                byte[] hashBytes = Encoding.UTF8.GetBytes(strHash.Substring(0, 20));
                hashBytes.CopyTo(payLoadBytes, 1);
                string strProofAddress = Base58.EncodeWithCheckSum(payLoadBytes);

                CoinRPC a = new CoinRPC(new Uri(GetURL(coinIP[cmbCoinType.Text]) + ":" + coinPort[cmbCoinType.Text]), new NetworkCredential(coinUser[cmbCoinType.Text], coinPassword[cmbCoinType.Text]));
                try
                {
                      a.ImportAddress(strProofAddress, openDigestFile.FileName, false);
                }
                catch
                {

                    DialogResult dialogResult = MessageBox.Show("This blockchain does not support file proof Lookups at this time.", "Limited functions", MessageBoxButtons.OK);
                    if (dialogResult == DialogResult.No)
                    {
                        return;
                    }
                }

                CreateLedgerFile(coinPayloadByteSize[cmbCoinType.Text], GetRandomBuffer(coinPayloadByteSize[cmbCoinType.Text]), coinIP[cmbCoinType.Text], coinPort[cmbCoinType.Text], coinUser[cmbCoinType.Text], coinPassword[cmbCoinType.Text], cmbWalletLabel.Text, coinVersion[cmbCoinType.Text], coinMinTransaction[cmbCoinType.Text], "", null, strHash + "\n" + "FileName:" + openDigestFile.FileName);

            }

        }


        private void rPCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Uri BrowseURL = new Uri(System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "/info.html");
            webBrowser1.Url = BrowseURL;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
                imgOpenLeft.Visible = false;
                imgOpenRight.Visible = true;
                splitContainer2.Panel1Collapsed = true;
  
        }

        private void imgOpenRight_Click(object sender, EventArgs e)
        {
            imgOpenLeft.Visible = true;
            imgOpenRight.Visible = false;
            splitContainer2.Panel1Collapsed = false;
        }

        private void imgBackButton_Click(object sender, EventArgs e)
        {
            webBrowser1.GoBack();
        }

        private void imgNextButton_Click(object sender, EventArgs e)
        {
            webBrowser1.GoForward();
        }
        private void RefreshNavBtn()
        {
            if (webBrowser1.CanGoBack == true) { imgBackButton.Image = Properties.Resources.OpenLeft; }
            else { imgBackButton.Image = Properties.Resources.OpenLeftDisabled; }

            if (webBrowser1.CanGoForward == true) { imgNextButton.Image = Properties.Resources.OpenRight; }
            else { imgNextButton.Image = Properties.Resources.OpenRightDisabled; }

            Match match = Regex.Match(webBrowser1.Url.OriginalString, @"/([a-fA-F0-9]{64})", RegexOptions.RightToLeft);
            if (match.Success && webBrowser1.Url.OriginalString.StartsWith("file"))
             {
                 txtTransIDSearch.Text = match.Value.TrimStart('/');
             }
             else { txtTransIDSearch.Text = webBrowser1.Url.OriginalString; }
        }

        private void webBrowser1_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            RefreshNavBtn();
            
             if (System.IO.Directory.Exists("root\\" + txtTransIDSearch.Text))
             { imgTrash.Image = Properties.Resources.Trash; imgTrash.Enabled = true; }
             else { imgTrash.Image = Properties.Resources.TrashDisabled; imgTrash.Enabled = false; }
             
             if (webBrowser1.DocumentText == "")
             {   imgApertusSplash.Visible = true;
                 txtInfoBox.Visible = true;
                 txtTransIDSearch.ForeColor = System.Drawing.Color.Gray;
                 txtTransIDSearch.Text = "ENTER SEARCH STRING";
             }
             else
             {
                 txtInfoBox.Visible = false;
                 imgApertusSplash.Visible = false;
                 txtTransIDSearch.ForeColor = System.Drawing.Color.Black;
             }

        }

        private void imgOpenUp_Click(object sender, EventArgs e)
        {
            imgOpenUp.Visible = false;
            imgOpenDown.Visible = true;
            splitContainer1.Panel2Collapsed = false;
        }

        private void imgOpenDown_Click(object sender, EventArgs e)
        {
            imgOpenUp.Visible = true;
            imgOpenDown.Visible = false;
            splitContainer1.Panel2Collapsed = true;
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            Uri BrowseURL = new Uri(System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "/root/catalog.htm");
            webBrowser1.Url = BrowseURL;
        }

        private void pictureBox4_Click(object sender, EventArgs e)
        {
            if (webBrowser1.Url != null)
            { treeView1.Nodes["root"].Nodes["favorites"].Nodes.Add(txtTransIDSearch.Text); }
        }

        private void treeView1_DoubleClick(object sender, EventArgs e)
        {
            txtTransIDSearch.Text = treeView1.SelectedNode.Text;
            Match match = Regex.Match(treeView1.SelectedNode.Text, @"([a-fA-F0-9]{64})", RegexOptions.RightToLeft);
            if (match.Success)
            {
                performSearch(true);
            }
            else {webBrowser1.Url = new Uri(txtTransIDSearch.Text);}

        }

        private void imgTrash_Click(object sender, EventArgs e)
        {
            if (System.IO.Directory.Exists("root\\" + txtTransIDSearch.Text))
            { System.IO.Directory.Delete("root\\" + txtTransIDSearch.Text, true);
            webBrowser1.DocumentText = "";
            imgTrash.Image = Properties.Resources.TrashDisabled;
            imgTrash.Enabled = false;


            }
        }
     }
}
