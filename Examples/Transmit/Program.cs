using System;
using PCSC;
using PCSC.Iso7816;

namespace Transmit
{
    public class Program
    {
        public static void Main() {
            using (var context = new SCardContext()) {
                context.Establish(SCardScope.System);

                var readerNames = context.GetReaders();
                if (readerNames == null || readerNames.Length < 1) {
                    Console.WriteLine("You need at least one reader in order to run this example.");
                    return;
                }

                var readerName = ChooseRfidReader(readerNames);
                if (readerName == null) {
                    return;
                }

                using (var rfidReader = new SCardReader(context)) {

                    var sc = rfidReader.Connect(readerName, SCardShareMode.Shared, SCardProtocol.T1);
                    if (sc != SCardError.Success) {
                        Console.WriteLine("Could not connect to reader {0}:\n{1}",
                            readerName,
                            SCardHelper.StringifyError(sc));
                        Console.ReadKey();
                        return;
                    }
                    
                    var apdu = new CommandApdu(IsoCase.Case2Short, rfidReader.ActiveProtocol) {
                        CLA = 0xFF,
                        Instruction = InstructionCode.GetData,
                        P1 = 0x00,
                        P2 = 0x00,
                        Le = 0  // We don't know the ID tag size
                    };

                    //sc = rfidReader.BeginTransaction();
                    //if (sc != SCardError.Success) {
                    //    Console.WriteLine("Could not begin transaction.");
                    //    Console.ReadKey();
                    //    return;
                    //}

                    Console.WriteLine("Retrieving the UID .... ");

					var receiveBuffer = new byte[257];
					var command = apdu.ToArray();

/*
					var sendPci = SCardPCI.GetPci(rfidReader.ActiveProtocol);

                    sc = rfidReader.Transmit(
                        sendPci,            // Protocol Control Information (T0, T1 or Raw)
                        command,            // command APDU
                        null,         // returning Protocol Control Information
                        ref receiveBuffer); // data buffer

                    if (sc != SCardError.Success) {
                        Console.WriteLine("Error: " + SCardHelper.StringifyError(sc));
                    }

                    var responseApdu = new ResponseApdu(receiveBuffer, IsoCase.Case2Short, rfidReader.ActiveProtocol);

*/

					int nr = receiveBuffer.Length;
					sc = rfidReader.Transmit(command, receiveBuffer, ref nr);

					if (sc != SCardError.Success)
					{
						Console.WriteLine("Error: " + SCardHelper.StringifyError(sc));
					}

					var responseApdu = new ResponseApdu(receiveBuffer, nr, IsoCase.Case2Short, rfidReader.ActiveProtocol);

/**/

					Console.WriteLine("SW1: {0:X2}, SW2: {1:X2}", responseApdu.SW1, responseApdu.SW2);
					Console.WriteLine("UID: {0}", responseApdu.HasData ? BitConverter.ToString(responseApdu.GetData()) : "No uid received");

                    //rfidReader.EndTransaction(SCardReaderDisposition.Leave);
                    rfidReader.Disconnect(SCardReaderDisposition.Reset);
                }
            }
        }

        private static string ChooseRfidReader(string[] readerNames) {
            // Show available readers.
            Console.WriteLine("Available readers: ");
            for (var i = 0; i < readerNames.Length; i++) {
                Console.WriteLine("[" + i + "] " + readerNames[i]);
            }

			if (readerNames.Length == 1)
			{
				Console.WriteLine("Using reader #0");
				return readerNames[0];
			}
			
			// Ask the user which one to choose.
            Console.Write("Which reader is an RFID reader? ");
            var line = Console.ReadLine();
            int choice;

            if (!(int.TryParse(line, out choice)) || (choice < 0) || (choice > readerNames.Length)) {
                Console.WriteLine("An invalid number has been entered.");
                Console.ReadKey();
                return null;
            }

            return readerNames[choice];
        }
    }
}