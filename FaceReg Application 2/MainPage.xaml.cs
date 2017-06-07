using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Microsoft;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

using System.Threading;


using Windows.Graphics.Imaging;
using Windows.Media;

using Windows.Media.FaceAnalysis;

using Windows.System.Threading;

using Windows.UI.Xaml.Shapes;
using Emmellsoft.IoT.Rpi.SenseHat;
using Windows.UI;

namespace FaceRegApplication2
{
    public sealed partial class MainPage : Page
    {
        private MediaCapture mediaCapture;



        private bool bIsPreviewing;

        User user = new User();



        private StorageFile sfPhotoFile;
        private readonly string PHOTO_FILE_NAME = "photo.jpg";
        private static bool phototake = false;
        private Rectangle box = new Rectangle();
        private readonly IFaceServiceClient faceServiceClient = new FaceServiceClient("bcba6dd40af64f31b200339e2322bf1c");
        private string personGroupId = "haxxorgroup";
        private Stream s;
        private ISenseHat senseHat;


        /// <summary>
        /// Brush for drawing the bounding box around each identified face.
        /// </summary>
        private readonly SolidColorBrush lineBrush = new SolidColorBrush(Windows.UI.Colors.Aqua);

        /// <summary>
        /// Thickness of the face bounding box lines.
        /// </summary>
        private readonly double lineThickness = 5.0;

        /// <summary>
        /// Transparent fill for the bounding box.
        /// </summary>
        private readonly SolidColorBrush fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);


        /// <summary>
        /// Holds the current scenario state value.
        /// </summary>
        // private ScenarioState currentState;



        /// <summary>
        /// Cache of properties from the current MediaCapture device which is used for capturing the preview frame.
        /// </summary>
        private VideoEncodingProperties videoProperties;

        /// <summary>
        /// References a FaceTracker instance.
        /// </summary>
        private FaceTracker faceTracker;

        /// <summary>
        /// A periodic timer to execute FaceTracker on preview frames
        /// </summary>
        private ThreadPoolTimer frameProcessingTimer;

        /// <summary>
        /// Semaphore to ensure FaceTracking logic only executes one at a time
        /// </summary>
        private SemaphoreSlim frameProcessingSemaphore = new SemaphoreSlim(1);

        public MainPage()
        {
            this.InitializeComponent();


            previewElement.Visibility = Visibility.Visible;

            facetrack();
            identifyBox.IsReadOnly = true;
            startCameraPreview();

            getsense();
      

            
        }

        private void getsense()
        {
            try

            {
                Task.Run(async () =>
                {
                    senseHat = await SenseHatFactory.GetSenseHat().ConfigureAwait(false);
                }).ConfigureAwait(false);


            }
            catch (ArgumentOutOfRangeException e)
            {
                System.Diagnostics.Debug.WriteLine(e.ActualValue);
            }

        }

        private void setYellow()
        {
            
            senseHat.Display.Fill(Colors.Yellow);
            senseHat.Display.Update();
          
          
        }
        private void setRed()
        {
            senseHat.Display.Fill(Colors.DarkRed);
            senseHat.Display.Update();

        }

        private void setGreen()
        {
            senseHat.Display.Fill(Colors.DarkGreen);
            senseHat.Display.Update();

        }





        private async void facetrack()
        {
            if (this.faceTracker == null)
            {
                this.faceTracker = await FaceTracker.CreateAsync();
            }
        }


        private async void startCameraPreview()
        {
            try
            {
                if (mediaCapture != null)
                {
                    // Cleanup MediaCapture object
                    if (bIsPreviewing)
                    {
                        await mediaCapture.StopPreviewAsync();
                        previewElement.Source = null;
                        bIsPreviewing = false;
                    }
                    mediaCapture.Dispose();
                    mediaCapture = null;
                }

                // Use default initialization
                mediaCapture = new MediaCapture();
                MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
                settings.StreamingCaptureMode = StreamingCaptureMode.Video;
                await this.mediaCapture.InitializeAsync(settings);

                // Cache the media properties as we'll need them later.
                var deviceController = this.mediaCapture.VideoDeviceController;
                this.videoProperties = deviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;

                // Start Preview                
                previewElement.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                bIsPreviewing = true;


                // Use a 66 millisecond interval for our timer, i.e. 15 frames per second
                TimeSpan timerInterval = TimeSpan.FromMilliseconds(66);
                this.frameProcessingTimer = Windows.System.Threading.ThreadPoolTimer.CreatePeriodicTimer(new Windows.System.Threading.TimerElapsedHandler(ProcessCurrentVideoFrame), timerInterval);


            }

            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Unable to initialize camera for audio/video mode: " + ex.Message);
            }
        }

        private void SetupVisualization(Windows.Foundation.Size framePizelSize, IList<DetectedFace> foundFaces)
        {
            this.VisualizationCanvas.Children.Clear();

            double actualWidth = this.VisualizationCanvas.ActualWidth;
            double actualHeight = this.VisualizationCanvas.ActualHeight;

            if (foundFaces != null && actualWidth != 0 && actualHeight != 0)
            {
                double widthScale = framePizelSize.Width / actualWidth;
                double heightScale = framePizelSize.Height / actualHeight;

                foreach (DetectedFace face in foundFaces)
                {
                    // Create a rectangle element for displaying the face box but since we're using a Canvas
                    // we must scale the rectangles according to the frames's actual size.
                    box = new Rectangle();
                    box.Width = (uint)(face.FaceBox.Width / widthScale);
                    box.Height = (uint)(face.FaceBox.Height / heightScale);
                    box.Fill = this.fillBrush;
                    box.Stroke = this.lineBrush;
                    box.StrokeThickness = this.lineThickness;
                    box.Margin = new Thickness((uint)(face.FaceBox.X / widthScale), (uint)(face.FaceBox.Y / heightScale), 0, 0);

                    this.VisualizationCanvas.Children.Add(box);

                    takeFrame();



                }
            }
        }

        private async void takeFrame()
        {


                if (box.Width > 140 && box.Height > 140 && !phototake)
                {                                                     // KOlla hur stor rektangeln e , plocka kod härifrån
                    
                  setYellow();
                
            

                    phototake = true;
                    sfPhotoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(
                    PHOTO_FILE_NAME,
                    CreationCollisionOption.GenerateUniqueName);

                    ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
                    await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, sfPhotoFile);

                    await Task.Run(() =>
                    {
                        Task.Yield();
                      
                            s = File.OpenRead(sfPhotoFile.Path);              
                        

                    }
                       );
                    identifyBox.Text = "Identifying...";
                    var faces = await faceServiceClient.DetectAsync(s);
                    var faceIds = faces.Select(face => face.FaceId).ToArray();

                try
                {
                    var results = await faceServiceClient.IdentifyAsync(personGroupId, faceIds);
                    foreach (var identifyResult in results)
                    {
                      


                        if (identifyResult.Candidates.Length == 0)
                        {
                            identifyBox.Text = "Identified as: No one";
                        }
                        else
                        {
                            // Get top 1 among all candidates returned
                            var candidateId = identifyResult.Candidates[0].PersonId;
                            var ress = identifyResult.Candidates[0].Confidence;
                            var person = await faceServiceClient.GetPersonAsync(personGroupId, candidateId);

                            identifyBox.Text = "Identified as: " + person.Name;

                            setGreen();
                        }
                    }
                }
                catch (Exception e)
                {

                    identifyBox.Text = "Unexcepted error, please try again";
                    String temp1 = sfPhotoFile.Path;
                    await Task.Run(() =>
                    {
                        Task.Yield();

                        File.Delete(temp1);

                    });
                  
                    takeFrame();
                }         

                    await Task.Delay(3000);
                 setRed();


                phototake = false;
                    identifyBox.Text = "";
                    s.Dispose();
                    String temp2 = sfPhotoFile.Path;
                    await Task.Run(() =>
                    {
                        Task.Yield();

                        File.Delete(temp2);

                    }
                       );

                }



            
            


        }
        private async void ProcessCurrentVideoFrame(ThreadPoolTimer timer)
        {




            // If a lock is being held it means we're still waiting for processing work on the previous frame to complete.
            // In this situation, don't wait on the semaphore but exit immediately.
            if (!frameProcessingSemaphore.Wait(0))
            {
                return;
            }

            try
            {
                IList<DetectedFace> faces = null;

                // Create a VideoFrame object specifying the pixel format we want our capture image to be (NV12 bitmap in this case).
                // GetPreviewFrame will convert the native webcam frame into this format.
                const BitmapPixelFormat InputPixelFormat = BitmapPixelFormat.Nv12;
                using (VideoFrame previewFrame = new VideoFrame(InputPixelFormat, (int)this.videoProperties.Width, (int)this.videoProperties.Height))
                {
                    await this.mediaCapture.GetPreviewFrameAsync(previewFrame);

                    // The returned VideoFrame should be in the supported NV12 format but we need to verify this.
                    if (FaceDetector.IsBitmapPixelFormatSupported(previewFrame.SoftwareBitmap.BitmapPixelFormat))
                    {
                        faces = await this.faceTracker.ProcessNextFrameAsync(previewFrame);
                    }
                    else
                    {
                        throw new System.NotSupportedException("PixelFormat '" + InputPixelFormat.ToString() + "' is not supported by FaceDetector");
                    }

                    // Create our visualization using the frame dimensions and face results but run it on the UI thread.
                    var previewFrameSize = new Windows.Foundation.Size(previewFrame.SoftwareBitmap.PixelWidth, previewFrame.SoftwareBitmap.PixelHeight);
                    var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        this.SetupVisualization(previewFrameSize, faces);

                    });
                }
            }
            catch (Exception ex)
            {
                var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    //  this.rootPage.NotifyUser(ex.ToString(), NotifyType.ErrorMessage);
                });
            }
            finally
            {
                frameProcessingSemaphore.Release();
            }

        }

    }
}

