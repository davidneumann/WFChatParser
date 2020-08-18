using AdysTech.CredentialManager;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ChatLoggerUtil
{
    class Program
    {
        static void Main(string[] args)
        {
            GetCrednetials(false);
        }

        private static void GetCrednetials(bool outputPasswords)
        {
            IConfiguration config = new ConfigurationBuilder()
                 .AddJsonFile("appsettings.json", true, true)
                 .AddJsonFile("appsettings.development.json", true, true)
                 .AddJsonFile("appsettings.production.json", true, true)
                 .Build();

            var key = config["Credentials:Key"];
            var salt = config["Credentials:Salt"];

            foreach (var i in config.GetSection("Launchers").GetChildren().AsEnumerable())
            {
                var section = config.GetSection(i.Path);

                var Password = outputPasswords ? GetPassword(config["Credentials:Key"], config["Credentials:Salt"], section.GetSection("WarframeCredentialsTarget").Value) : string.Empty;
                var Username = GetUsername(config["Credentials:Key"], config["Credentials:Salt"], section.GetSection("WarframeCredentialsTarget").Value);
                Console.WriteLine($"{Username} : {Password}");
            }
        }

        private static string GetPassword(string key, string salt, string target)
        {
            var r = CredentialManager.GetCredentials(target, CredentialManager.CredentialType.Generic);
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.Password);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }
        private static string GetUsername(string key, string salt, string target)
        {
            var r = CredentialManager.GetCredentials(target, CredentialManager.CredentialType.Generic);
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.UserName);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }
    }
}
