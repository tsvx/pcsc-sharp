using System;
using PCSC;
using PCSC.Iso7816;
using System.Text;

namespace MonitorReaderEvents
{
	public static class Program
	{

		public static void Main()
		{
			Console.WriteLine("This program will monitor all SmartCard readers and display all status changes.");
			Console.WriteLine("Press a key to continue.");
			Console.ReadKey(); // Wait for user to press a key

			// Retrieve the names of all installed readers.
			string[] readerNames;
			using (var context = new SCardContext())
			{
				context.Establish(SCardScope.System);
				readerNames = context.GetReaders();

				if (readerNames == null || readerNames.Length < 1)
				{
					Console.WriteLine("There are currently no readers installed.");
					return;
				}

				Console.WriteLine("Readers:");
				foreach(var reader in readerNames)
				{
					Console.WriteLine(reader);
					DisplayReaderInfo(context, reader);
				}

				// Create a monitor object with its own PC/SC context. 
				// The context will be released after monitor.Dispose()
				var monitor = new SCardMonitor(new SCardContext(), SCardScope.System);
				// Point the callback function(s) to the anonymous & static defined methods below.
				monitor.CardInserted += (sender, args) =>
				{
					DisplayEvent("CardInserted", args);
					DisplayUid(context, args.ReaderName);
				};
				monitor.CardRemoved += (sender, args) => DisplayEvent("CardRemoved", args);
				monitor.Initialized += (sender, args) =>
				{
					DisplayEvent("Initialized", args);
					//DisplayReaderSerial(context, args.ReaderName);
				};
				monitor.StatusChanged += StatusChanged;
				monitor.MonitorException += MonitorException;

				foreach (var reader in readerNames)
				{
					Console.WriteLine("Start monitoring for reader " + reader + ".");
				}

				monitor.Start(readerNames);

				// Let the program run until the user presses a key
				Console.ReadKey();

				// Stop monitoring
				monitor.Cancel();

				// Dispose monitor resources (SCardContext)
				monitor.Dispose();
			}
		}

		private static void DisplayEvent(string eventName, CardStatusEventArgs unknown)
		{
			Console.WriteLine(">> {0} Event for reader: {1}", eventName, unknown.ReaderName);
			Console.WriteLine("ATR: {0}", BitConverter.ToString(unknown.Atr ?? new byte[0]));
			Console.WriteLine("State: {0}\n", unknown.State);
		}

		private static void StatusChanged(object sender, StatusChangeEventArgs args)
		{
			Console.WriteLine(">> StatusChanged Event for reader: {0}", args.ReaderName);
			Console.WriteLine("ATR: {0}", BitConverter.ToString(args.Atr ?? new byte[0]));
			Console.WriteLine("Last state: {0}\nNew state: {1}\n", args.LastState, args.NewState);
		}

		private static void MonitorException(object sender, PCSCException ex)
		{
			Console.WriteLine("Monitor exited due an error:");
			Console.WriteLine(SCardHelper.StringifyError(ex.SCardError));
		}

		private static void DisplayReaderInfo(SCardContext context, string readerName)
		{
			using (var rfidReader = new SCardReader(context))
			{
				var sc = rfidReader.Connect(readerName, SCardShareMode.Direct, SCardProtocol.Unset);
				if (sc != SCardError.Success)
				{
					Console.WriteLine("Could not connect to reader {0}:\n{1}",
						readerName,
						SCardHelper.StringifyError(sc));
					Console.ReadKey();
					return;
				}

				rfidReader.DisplayInfo("Serial Number", SCardAttribute.VendorInterfaceDeviceTypeSerialNumber);
				rfidReader.DisplayInfo("Device Type", SCardAttribute.VendorInterfaceDeviceType);
				rfidReader.DisplayInfo("Device Version", SCardAttribute.VendorInterfaceDeviceTypeVersion, BitConverter.ToString);
				rfidReader.DisplayInfo("Vendor", SCardAttribute.VendorName);
				rfidReader.DisplayInfo("Device Name", SCardAttribute.DeviceFriendlyNameA);

				rfidReader.Disconnect(SCardReaderDisposition.Leave);
			}
		}

		private static void DisplayInfo(this SCardReader reader, string title, SCardAttribute attribute, Func<byte[], string> formatter = null)
		{
			Console.WriteLine("Retrieving the {0} .... ", title);
			if (formatter == null)
				formatter = bytes => Encoding.ASCII.GetString(bytes, 0, 1 + Array.FindLastIndex(bytes, b => b != 0));

			byte[] data;
			var sc = reader.GetAttrib(attribute, out data);
			data = data ?? new byte[] { };


			if (sc != SCardError.Success)
			{
				Console.WriteLine("Error by trying to receive the {0}. {1}\n", title, SCardHelper.StringifyError(sc));
			}
			else
			{
				Console.WriteLine(title + ": {0}\n", formatter(data));
			}
		}

		private static void DisplayUid(SCardContext context, string readerName)
		{
			using (var rfidReader = new SCardReader(context))
			{
				var sc = rfidReader.Connect(readerName, SCardShareMode.Shared, SCardProtocol.Any);
				if (sc != SCardError.Success)
				{
					Console.WriteLine("Could not connect to reader {0}:\n{1}",
						readerName,
						SCardHelper.StringifyError(sc));
					Console.ReadKey();
					return;
				}

				var apdu = new CommandApdu(IsoCase.Case2Short, rfidReader.ActiveProtocol)
				{
					CLA = 0xFF,
					Instruction = InstructionCode.GetData,
					P1 = 0x00,
					P2 = 0x00,
					Le = 0  // We don't know the ID tag size
				};

				sc = rfidReader.BeginTransaction();
				if (sc != SCardError.Success)
				{
					Console.WriteLine("Could not begin transaction.");
					Console.ReadKey();
					return;
				}

				Console.WriteLine("Retrieving the UID .... ");

				var receivePci = new SCardPCI(); // IO returned protocol control information.
				var sendPci = SCardPCI.GetPci(rfidReader.ActiveProtocol);

				var receiveBuffer = new byte[256];
				var command = apdu.ToArray();

				sc = rfidReader.Transmit(
					sendPci,            // Protocol Control Information (T0, T1 or Raw)
					command,            // command APDU
					receivePci,         // returning Protocol Control Information
					ref receiveBuffer); // data buffer

				if (sc != SCardError.Success)
				{
					Console.WriteLine("Error: " + SCardHelper.StringifyError(sc));
				}

				var responseApdu = new ResponseApdu(receiveBuffer, IsoCase.Case2Short, rfidReader.ActiveProtocol);
				Console.WriteLine("SW1: {0:X2}, SW2: {1:X2}", responseApdu.SW1, responseApdu.SW2);
				Console.WriteLine("Uid: {0}", responseApdu.HasData ? BitConverter.ToString(responseApdu.GetData()) : "No uid received");
				Console.WriteLine();

				rfidReader.EndTransaction(SCardReaderDisposition.Leave);
				rfidReader.Disconnect(SCardReaderDisposition.Reset);
			}
		}
	}
}