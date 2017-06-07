using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Popups;

namespace FaceRegApplication2
{
    public static class MessageHelper
    {
        public static async Task Message(string title, string content)
        {
            var dialog = new MessageDialog("");
            dialog.Title = title;
            dialog.Content = content;
            await dialog.ShowAsync();
        }

        public static async Task RegistrationSuccessful(string firstname, string lastname, string personNbr)
        {
            var dialog = new MessageDialog("");
            dialog.Title = "Registration Complete";
            dialog.Content = "Firstname: " + firstname + "\n" + "Lastname: " + lastname + "\n" + "Person Number: " + personNbr;
            await dialog.ShowAsync();
        }

        public static async Task RegistrationError(string firstname, string lastname, string personNbr, bool pictureTaken)
        {
            var dialog = new MessageDialog("");
            dialog.Title = "Insufficient Inforation";

            if (string.IsNullOrEmpty(firstname))    { dialog.Content += "- \t Please enter your firstname \n"; }
            if (string.IsNullOrEmpty(lastname))     { dialog.Content += "- \t Please enter your lastname \n"; }
            if (!pictureTaken)                      { dialog.Content += "- \t Please take a picture"; }



            await dialog.ShowAsync();
        }

        public static async Task TakePhotoError()
        {
            var dialog = new MessageDialog("");
            dialog.Title = "Insufficient Inforation";
            dialog.Content = "Please enter your person number first";


            await dialog.ShowAsync();
        }
    }
}
