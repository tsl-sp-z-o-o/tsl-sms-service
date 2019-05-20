using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TslWebApp.Utils
{
    internal sealed class SmsHelper
    {

        /// <summary>
		/// Encodes an SMS text message into the PDU format.
		/// </summary>
		/// <param name="number">The recipient's phone number (destination address)</param>
		/// <param name="message">The message text (user data text)</message>
		/// <param name="smscAddress">Number of the service center. If an
		/// empty string, address stored in the phone will be used.</param>
		/// <param name="actualLength">Receives the actual length of the PDU data.</param>
		/// <returns>The encoded message</returns>
		public static string GetPdu(string number, 
                                    string message, 
                                    string smscAddress,
                                    out int actualLength)
        {
            const byte internationalFormat = 0x91; // telephone/ISDN, international format
            const byte unknownFormat = 0x81; // telephone/ISDN, unknown format

            // Determine destination address type
            byte numberAddressType;
            string useNumber;
            if (number.StartsWith("+"))
            {
                numberAddressType = internationalFormat;
                useNumber = number.Substring(1); // strip initial "+" character
            }
            else
            {
                numberAddressType = unknownFormat;
                useNumber = number;
            }

            // Determine SMSC address type
            byte smscAddressType;
            string useSmscAddress;
            if (smscAddress.StartsWith("+"))
            {
                smscAddressType = internationalFormat;
                useSmscAddress = smscAddress.Substring(1); // strip initial "+" character
            }
            else
            {
                smscAddressType = unknownFormat;
                useSmscAddress = smscAddress;
            }

            // Encode the user data text
            byte dataLength;
            try
            {
                byte[] encodedText = EncodeText(message, out dataLength);
            
            // Encode the SMSC address
            string encodedSmsc = EncodeSmscAddress(useSmscAddress, smscAddressType);

            // Create the PDU string
            StringBuilder builder = new StringBuilder();
            builder.Append(IntToHex(0x11)); // message flags:
                                            //   msg type=SMS-SUBMIT-PDU,
                                            //   validity=relative
            builder.Append(IntToHex(0x00)); // Message Reference, 0=set by device
            builder.Append(EncodeDestinationAddress(useNumber, numberAddressType));
            builder.Append(IntToHex(0x00)); // Protocol ID, 0=SME-to-SME
            builder.Append(IntToHex(0x00)); // Data Coding Scheme, 0=7-Bit default
            builder.Append(IntToHex(0xA7)); // Relative Validity, 0xA7=24 hrs.
            builder.Append(IntToHex(dataLength));
            builder.Append(IntToHex(encodedText));

            actualLength = builder.Length / 2;
            builder.Insert(0, encodedSmsc);
            return builder.ToString();
            }
            catch (ArgumentException ex)
            {
                actualLength = -1;
                return ex.Message;
            }
        }

        private static string EncodeSmscAddress(string address, byte addressType)
        {
            if (address.Length == 0)
                return IntToHex(0);

            string encoded = EncodeSemiOctets(address);

            // count bytes
            byte lenBytes = (byte)((encoded.Length / 2) + 1); // plus prefix byte
            return IntToHex(lenBytes) + IntToHex(addressType) + encoded;
        }

        private static string EncodeDestinationAddress(string address, byte addressType)
        {
            if (address.Length == 0)
            {
                // special case: specify length 0 BUT include the type!
                return IntToHex(0) + IntToHex(addressType);
            }

            // count characters
            byte lenChars = (byte)address.Length; // count original characters
            string encoded = EncodeSemiOctets(address);
            return IntToHex(lenChars) + IntToHex(addressType) + encoded;
        }

        private static byte[] EncodeText(string data, out byte dataLength)
        {
            const int maxSeptets = 160;
            data = StringTo7Bit(data);
            int length = data.Length;
            if (data.Length > maxSeptets)
            {
                throw new ArgumentException("Text is too long. A maximum of " +
                maxSeptets.ToString() + " septets is allowed. " + length.ToString() +
                " were passed.");
            }
            dataLength = (byte)length;
            return SeptetsToOctetsInt(data);
        }

        // BCD operations

        /// <summary>
        /// Swaps the semi-octets of a BCD encoded string.
        /// </summary>
        /// <param name="data">The string to convert.</param>
        /// <remarks>
        /// <para>If the string is not of even length, it is padded with a
        /// hexadecimal "F" before converting.</para>
        /// <para>This method does not verify the actual contents of the string.</para>
        /// </remarks>
        /// <returns>The converted value.</returns>
        /// <example>
        /// <param>A string containing "12345678" will become "21436587".</param>
        /// <param>A string containing "1234567" will become "214365F7".</param>
        /// </example>
        private static string EncodeSemiOctets(string data)
        {
            if (data.Length % 2 != 0)
                data += "F"; // Pad address with an "F" to make it even length

            string swapped = string.Empty;
            for (int i = 0; i < data.Length; i += 2)
                swapped += data.Substring(i + 1, 1) + data.Substring(i, 1);
            return swapped;
        }

        // Some numeric conversions

        private static char[] hexDigits = {
        '0', '1', '2', '3', '4', '5', '6', '7',
        '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'};

        /// <summary>
        /// Converts a byte array into its hexadecimal representation (BCD encoding).
        /// </summary>
        /// <param name="bytes">The byte array to convert.</param>
        /// <returns>The converted value.</returns>
        private static string IntToHex(byte[] bytes)
        {
            char[] chars = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int b = bytes[i];
                chars[i * 2] = hexDigits[b >> 4];
                chars[i * 2 + 1] = hexDigits[b & 0xF];
            }
            return new string(chars);
        }

        /// <summary>
        /// Converts a byte into its BCD (hexadecimal) representation.
        /// </summary>
        /// <param name="b">The byte to convert.</param>
        /// <returns>The converted value.</returns>
        private static string IntToHex(byte b)
        {
            return hexDigits[b >> 4].ToString() + hexDigits[b & 0xF].ToString();
        }

        /// <summary>
        /// Converts a bit string into a byte.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>The converted value.</returns>
        private static byte BinToInt(string s)
        {
            return Convert.ToByte(s, 2);
        }

        /// <summary>
        /// Converts a byte into a bit string.
        /// </summary>
        /// <param name="b">The byte to convert.</param>
        /// <param name="size">
        /// The final length the string should have. If the resulting string is
        /// shorter than this value, it is padded with leading zeroes.
        /// </param>
        /// <returns>The converted value.</returns>
        private static string IntToBin(byte b, byte size)
        {
            return Convert.ToString(b, 2).PadLeft(size, '0');
        }

        // Text data conversion

        /// <summary>
        /// Converts a character from the ISO-8859-1 character set
        /// into the corresponding character of the GSM "7-bit default alphabet"
        /// character set.
        /// </summary>
        /// <param name="c">The character to convert.</param>
        /// <returns>A string containing the converted character.</returns>
        /// <remarks>
        /// A string is returned instead of a character because some characters
        /// must be escaped, and consist then of two characters instead of one.
        /// </remarks>
        private static string CharTo7Bit(char c)
        {
            byte retval;
            bool escape = false;
            switch (c)
            {
                // Characters not listed here are equal to those in the
                // ISO-8859-1 charset OR not present in it.

                case '@': retval = 0; break;
                case '£': retval = 1; break;
                case '$': retval = 2; break;
                case '¥': retval = 3; break;
                case 'è': retval = 4; break;
                case 'é': retval = 5; break;
                case 'ú': retval = 6; break;
                case 'ì': retval = 7; break;
                case 'ò': retval = 8; break;
                case 'Ç': retval = 9; break;
                case 'Ø': retval = 11; break;
                case 'ø': retval = 12; break;
                case 'Å': retval = 14; break;
                case 'å': retval = 15; break;
                case '_': retval = 17; break;
                case 'Æ': retval = 28; break;
                case 'æ': retval = 29; break;
                case 'ß': retval = 30; break;
                case 'É': retval = 31; break;
                case '¤': retval = 36; break; // 164 in ISO-8859-1
                case '¡': retval = 64; break;
                // 65-90 capital letters
                case 'Ä': retval = 91; break;
                case 'Ö': retval = 92; break;
                case 'Ñ': retval = 93; break;
                case 'Ü': retval = 94; break;
                case '§': retval = 95; break;
                case '¿': retval = 96; break;
                // 97-122 small letters
                case 'ä': retval = 123; break;
                case 'ö': retval = 124; break;
                case 'ñ': retval = 125; break;
                case 'ü': retval = 126; break;
                case 'à': retval = 127; break;

                // extension table
                case '\f': retval = 10; escape = true; break; // form feed, 0x0C
                case '^': retval = 20; escape = true; break;
                case '{': retval = 40; escape = true; break;
                case '}': retval = 41; escape = true; break;
                case '\\': retval = 47; escape = true; break;
                case '[': retval = 60; escape = true; break;
                case '~': retval = 61; escape = true; break;
                case ']': retval = 62; escape = true; break;
                case '|': retval = 64; escape = true; break;
                case '€': retval = 101; escape = true; break; // 164 in ISO-8859-15

                default: retval = (byte)c; break;
            }
            return (escape ? Convert.ToChar(0x1B).ToString() : "") + Convert.ToChar(retval).ToString();
        }

        /// <summary>
        /// Converts a string consisting of characters from the ISO-8859-1
        /// character set into a string of corresponding characters of the
        /// GSM "7-bit default alphabet" character set.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>The converted string.</returns>
        /// <remarks>
        /// Note that the converted string does not need to have the same
        /// length as the original one because some characters may be escaped.
        /// </remarks>
        private static string StringTo7Bit(string s)
        {
            string newString = string.Empty;
            for (int i = 0; i < s.Length; i++)
                newString += CharTo7Bit(s.Substring(i, 1)[0]);
            return newString;
        }

        /// <summary>
        /// Compacts a string of septets into octets.
        /// </summary>
        /// <remarks>
        /// <par>When only 7 of 8 available bits of a character are used, 1 bit is
        /// wasted per character. This method compacts a string of characters
        /// which consist solely of such 7-bit characters.</par>
        /// <par>Effectively, every 8 bytes of the original string are packed into
        /// 7 bytes in the resulting string.</par>
        /// </remarks>
        private static byte[] SeptetsToOctetsInt(string data)
        {
            ArrayList output = new ArrayList();
            string octetSecond = string.Empty;
            for (int i = 0; i < data.Length; i++)
            {
                string current = IntToBin((byte)data[i], 7);
                if (i != 0 && i % 8 != 0)
                {
                    string octetFirst = current.Substring(7 - i % 8);
                    string currentOctet = octetFirst + octetSecond;
                    output.Add(BinToInt(currentOctet));
                }
                octetSecond = current.Substring(0, 7 - i % 8);
                if (i == data.Length - 1 && octetSecond != string.Empty)
                    output.Add(BinToInt(octetSecond));
            }

            byte[] array = new byte[output.Count];
            output.CopyTo(array);
            return array;
        }

        private static string FormatUdh(int length, int seqno)
        {
            byte[] uhd = new byte[6];
            uhd[0] = 5;
            uhd[1] = 0;
            uhd[2] = 3;
            uhd[3] = 0;
            uhd[4] = (byte)(length / 160);
            uhd[5] = (byte)seqno;
            return System.Text.Encoding.ASCII.GetString(uhd);
        }
    }
}
