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
            GetCrednetials(true);
            //SaveCredentials();
        }

        private static void SaveCredentials()
        {
            IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", true, true)
                    .AddJsonFile("appsettings.development.json", true, true)
                    .AddJsonFile("appsettings.production.json", true, true)
                    .Build();

            var key = config["Credentials:Key"];
            var salt = config["Credentials:Salt"];

            Console.Write("Target (ex, WFBot:Bot3): ");
            var target = Console.ReadLine();
            Console.Write("Username: ");
            var username = Console.ReadLine();
            Console.Write("\r\nPassword: ");
            var password = Console.ReadLine();


            string encryptedPassword = null;
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    var pass = Encoding.UTF8.GetBytes(password);
                    cs.Write(pass, 0, pass.Length);
                    cs.Close();
                }
                encryptedPassword = Convert.ToBase64String(ms.ToArray());
            }

            string encryptedUsername = null;
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    var pass = Encoding.UTF8.GetBytes(username);
                    cs.Write(pass, 0, pass.Length);
                    cs.Close();
                }
                encryptedUsername = Convert.ToBase64String(ms.ToArray());
            }

            CredentialManager.SaveCredentials(target, new System.Net.NetworkCredential(encryptedUsername, encryptedPassword));
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

                string target = section.GetSection("WarframeCredentialsTarget").Value;
                var Password = outputPasswords ? GetPassword(config["Credentials:Key"], config["Credentials:Salt"], target) : string.Empty;
                var Username = GetUsername(config["Credentials:Key"], config["Credentials:Salt"], target);
                Console.WriteLine($"{target} {Username} : {Password}");
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
