using System;
using System.Web;
using System.Collections;
using System.Security.Cryptography;
using System.Text;

//Used to mask the Sentry information
namespace Cherry.HelperClasses
{
    public class TDesEncryption
    {

        public TDesEncryption()
        {
            //Uncomment the following line if using designed components 
            //InitializeComponent(); 
        }


        public string Encrypt(string EncriptionText, string key)
        {

            TripleDES des = CreateDES(key);

            ICryptoTransform ct = des.CreateEncryptor();

            byte[] input = Encoding.Unicode.GetBytes(EncriptionText);
            return EncodeUrlString(Convert.ToBase64String(ct.TransformFinalBlock(input, 0, input.Length)));
        }

        public string Decrypt(string DecrytionText, string key)
        {

            byte[] b = Convert.FromBase64String(DecrytionText);
            TripleDES des = CreateDES(key);
            ICryptoTransform ct = des.CreateDecryptor();
            byte[] output = ct.TransformFinalBlock(b, 0, b.Length);
            return Encoding.Unicode.GetString(output);

        }

        public TripleDES CreateDES(string key)
        {

            MD5 md5 = new MD5CryptoServiceProvider();
            TripleDES des = new TripleDESCryptoServiceProvider();
            des.Key = md5.ComputeHash(Encoding.Unicode.GetBytes(key));
            des.IV = new byte[des.BlockSize / 8];
            return des;

        }

        public string EncodeUrlString(string encodeString)
        {
            return HttpUtility.UrlEncode(encodeString);
        }

        public string DecodeUrlString(string decodeString)
        {
            return HttpUtility.UrlDecode(decodeString);
        }

    }

}