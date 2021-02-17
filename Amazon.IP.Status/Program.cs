using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;

namespace Amazon.IP.Status
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            const string hostName = @"teamcenterWebClient.amazonaws.com";
            const string configurationFile = @"Amazon.IP.ini";
            const string logFile = @"Amazon.IP.log";

            List<IPAddress> currentIpAddresses = new List<IPAddress>();

            if (File.Exists(configurationFile))
            {
                using StreamReader streamReader = new StreamReader(configurationFile);

                while (!streamReader.EndOfStream)
                {
                    if (IPAddress.TryParse(streamReader.ReadLine() ?? throw new ArgumentNullException(),
                        out IPAddress currentIpAddress))
                    {
                        currentIpAddresses.Add(currentIpAddress);
                    }
                }

                streamReader.Close();
            }

            IPAddress[] newIpAddresses = Dns.GetHostEntry(hostName).AddressList;

            using StreamWriter logWriter = new StreamWriter(logFile, true);

            if (newIpAddresses.Intersect(currentIpAddresses).Any())
            {
                logWriter.WriteLine($"{DateTime.Now}: No Change");               
                return;
            }

            using StreamWriter streamWriter = new StreamWriter(configurationFile);

            foreach (IPAddress ipAddress in newIpAddresses)
            {
                logWriter.WriteLine($"{DateTime.Now}: {ipAddress}");
                streamWriter.WriteLine(ipAddress);
            }

            logWriter.Close();
            streamWriter.Close();

            SendEmail(GetIpString(currentIpAddresses), GetIpString(newIpAddresses));
        }

        private static string GetIpString(IReadOnlyList<IPAddress> ipAddresses)
        {
            string ipString = ipAddresses[0].ToString();

            for (int i = 1; i < ipAddresses.Count - 1; i++)
            {
                ipString += i == ipAddresses.Count - 1
                    ? i == 1
                        ? $" and {ipAddresses[i]}"
                        : $", and {ipAddresses[i]}"
                    : $", {ipAddresses[i]}";
            }

            return ipString;
        }

        private static void SendEmail(string currentIpString, string newIpString)
        {
            const string subject = "Amazon IP Address has rolled";

            string body = $"Amazon IP return addresses {currentIpString} are no longer valid.<br/>" +
                          $"Please add {newIpString} to the firewall exceptions.<br/><br/>" +
                          "The configuration file has been updated to reflect these changes. " +
                          $"New IP rollovers will not be recognized until {newIpString} roll over.";

            string hostAddress = Properties.Resources.host;
            string from = Properties.Resources.fromAddress;
            string password = Properties.Resources.password;

            string to = ConfigurationManager.AppSettings["toAddress"];

            using MailMessage mailMessage = new MailMessage
            {
                From = new MailAddress(@from),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
                Priority = MailPriority.High
            };

            string[] emailAddresses = to.Split(',');

            foreach (string emailAddress in emailAddresses)
            {
                mailMessage.To.Add(new MailAddress(emailAddress));
            }

            NetworkCredential networkCredentials = new NetworkCredential
            {
                UserName = mailMessage.From.Address,
                Password = password
            };

            using SmtpClient smtp = new SmtpClient
            {
                Host = hostAddress,
                Port = 587,
                EnableSsl = true,
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = networkCredentials
            };

            smtp.Send(mailMessage);
        }
    }
}
