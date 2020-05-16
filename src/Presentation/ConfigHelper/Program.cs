using AdysTech.CredentialManager;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ConfigHelper
{
    class Program
    {
        static void Main(string[] args)
        {
            var outputPath = "appsettings.production.json";
            var appsettings = new Appsettings();

            Console.WriteLine($"Config will be saved to {outputPath}");
            Console.WriteLine("Make sure to run this on the machine that will run the bot. " +
                "This will save your credentials to the Windows Credential Manager");

            Console.WriteLine("\nCredential manager encryption information");
            appsettings.Credentials.Key = PromptUser("Encryption key: ");
            appsettings.Credentials.Salt = PromptUser("Encryption salt: ");

            do
            {
                var launcher = new Launcher();
                Console.WriteLine("\nBot information");
                launcher.WarframeCredentialsTarget = PromptUser("Credential manager name/key (ex Bot1): ");
                launcher.Username = PromptUser("Windows account username: ");
                launcher.Password = PromptUser("Windows account password: ");
                launcher.Region = PromptUser("Bot identifier/region: ");

                var username = PromptUser("Warframe account username: ");
                var password = PromptUser("Warframe account password: ");
                SaveWarframeUsernamePassword(launcher.WarframeCredentialsTarget,
                    appsettings.Credentials.Key, appsettings.Credentials.Salt,
                    username, password);
                Console.WriteLine("Credentials saved to Windows Credential Store");

                appsettings.Launchers.Add(launcher);

                Console.Write("Add another bot? (y/n): ");
            } while (Console.ReadLine().Trim().ToLower() == "y");

            var json = JsonConvert.SerializeObject(appsettings);
            try
            {
                File.WriteAllText(outputPath, json);
                Console.WriteLine($"Config saved to {outputPath}");
            }
            catch
            {
                Console.WriteLine($"Failed to save to {outputPath}\n");
                Console.WriteLine(json);
            }

            Console.WriteLine("**Manually** edit the LauncherPath!!");

            Console.WriteLine("\nPress any key to quit.");
            Console.ReadKey();
        }

        private static string PromptUser(string message)
        {
            Console.Write(message.Trim().TrimEnd(':') + ": ");
            return Console.ReadLine().Trim();
        }

        private static void SaveWarframeUsernamePassword(string target,
            string key, string salt,
            string username, string password)
        {
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
    }
}
