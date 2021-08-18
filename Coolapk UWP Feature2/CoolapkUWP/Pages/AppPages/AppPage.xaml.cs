﻿using CoolapkUWP.Control.ViewModels;
using CoolapkUWP.Data;
using CoolapkUWP.Pages.FeedPages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.UserActivities;
using Windows.Data.Json;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace CoolapkUWP.Pages.AppPages
{
    /// <summary>
    /// 这是小板子传承的最后的东西了，只有这个页面从第一个版本流传至今，希望后人不要把它删了(●'◡'●)
    /// </summary>
    public sealed partial class AppPage : Page, INotifyPropertyChanged
    {
        private AppModel appmodels;
        private AppModel AppModels
        {
            get => appmodels;
            set
            {
                appmodels = value;
                RaisePropertyChangedEvent();
            }
        }

        private string AppLink;

        public event PropertyChangedEventHandler PropertyChanged;

        internal void RaisePropertyChangedEvent([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            if (name != null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
        }

        public AppPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            //将传过来的数据 类型转换一下
            AppLink = e.Parameter as string;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(AppLink)) { return; }
            else { GetAppDetail(); }
        }

        private static (bool, IJsonValue) GetResult(string json)
        {
            JsonObject o = JsonObject.Parse(json);
            if (!string.IsNullOrEmpty(json) &&
                !o.TryGetValue("dataRow", out _) &&
                o.TryGetValue("message", out IJsonValue message))
            {
                UIHelper.ShowMessage(message.ToString());
                return (false, null);
            }
            else { return (true, o); }
        }

        private async void GetAppDetail()
        {
            //titleBar.ShowProgressRing();
            string result = await UIHelper.GetHTML(AppLink, "XMLHttpRequest");
            if (!string.IsNullOrEmpty(result))
            {
                (bool isNotNull, IJsonValue json) = GetResult(result);
                if (isNotNull)
                {
                    AppModels = new AppModel((JsonObject)json);
                    _ = GenerateActivityAsync();
                }
                else
                {
                    Regex regex = new Regex(@"https://www.coolapk.com/\w+/(\d+)");
                    if (regex.IsMatch(AppLink) && !string.IsNullOrEmpty(regex.Match(AppLink).Groups[1].Value))
                    {
                        string id = regex.Match(AppLink).Groups[1].Value;
                        Frame.GoBack();
                        UIHelper.Navigate(typeof(FeedListPage), new object[] { FeedListType.APPPageList, id });
                    }
                }
            }
            //titleBar.HideProgressRing();
        }

        private void CopyM_Click(object sender, RoutedEventArgs e)
        {

            if (sender is MenuFlyoutItem selectedItem)
            {
                DataPackage dp = new DataPackage();
                // copy 
                FrameworkElement element = sender as FrameworkElement;
                switch (element.Name)
                {
                    case "Introduce": dp.SetText(AppModels.Introduce); break;
                    case "Description": dp.SetText(AppModels.Description); break;
                    case "PackageName": dp.SetText(AppModels.PackageName); break;
                    case "Version": dp.SetText(AppModels.Version); break;
                    case "ChangeLog": dp.SetText(AppModels.ChangeLog); break;
                }
                Clipboard.SetContent(dp);
            }
        }

        void FeedPage_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            Uri shareLinkString = ValidateAndGetUri(AppLink);
            if (shareLinkString != null)
            {
                DataPackage dataPackage = new DataPackage();
                dataPackage.SetWebLink(shareLinkString);
                dataPackage.Properties.Title = "应用分享";
                dataPackage.Properties.Description = AppLink;
                DataRequest request = args.Request;
                request.Data = dataPackage;
            }
            else
            {
                DataPackage dataPackage = new DataPackage();
                dataPackage.SetText(AppLink);
                dataPackage.Properties.Title = "内容分享";
                dataPackage.Properties.Description = "内含文本";
                DataRequest request = args.Request;
                request.Data = dataPackage;
            }
        }

        private static Uri ValidateAndGetUri(string uriString)
        {
            Uri uri = null;
            try
            {
                uri = new Uri(uriString);
            }
            catch (FormatException)
            {
            }
            return uri;
        }

        protected void ShowUIButton_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested -= FeedPage_DataRequested;
            dataTransferManager.DataRequested += FeedPage_DataRequested;
            DataTransferManager.ShowShareUI();
        }

        private UserActivitySession _currentActivity;
        private async Task GenerateActivityAsync()
        {
            // Get the default UserActivityChannel and query it for our UserActivity. If the activity doesn't exist, one is created.
            UserActivityChannel channel = UserActivityChannel.GetDefault();
            UserActivity userActivity = await channel.GetOrCreateUserActivityAsync(UIHelper.GetMD5(AppLink));

            // Populate required properties
            userActivity.VisualElements.DisplayText = AppModels.Title;
            userActivity.VisualElements.AttributionDisplayText = AppModels.Title;
            userActivity.VisualElements.Description = AppModels.Introduce.Length > 3 ? AppModels.Introduce : AppModels.Description;
            userActivity.ActivationUri = new Uri("coolapk://" + AppLink);

            //Save
            await userActivity.SaveAsync(); //save the new metadata

            // Dispose of any current UserActivitySession, and create a new one.
            _currentActivity?.Dispose();
            _currentActivity = userActivity.CreateSession();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            switch (element.Name)
            {
                case "Report":
                    UIHelper.Navigate(typeof(BrowserPage), new object[] { false, "https://m.coolapk.com/mp/apk/report?apkname=" + AppModels.PackageName });
                    break;

                case "GotoUri":
                    _ = Launcher.LaunchUriAsync(new Uri(AppLink));
                    break;

                case "UPanel":
                case "ViewFeed":
                    UIHelper.Navigate(typeof(FeedListPage), new object[] { FeedListType.APPPageList, element.Tag.ToString() });
                    break;

                case "DownloadButton":
                    _ = Launcher.LaunchUriAsync(new Uri((sender as FrameworkElement).Tag.ToString()));
                    break;

                default:
                    UIHelper.OpenLink((sender as FrameworkElement).Tag as string);
                    break;
            }
        }

        private void PicA_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is GridView view)
            {
                if (view.SelectedIndex > -1 && view.Tag is List<string> ss)
                { UIHelper.ShowImages(ss.ToArray(), view.SelectedIndex); }
                view.SelectedIndex = -1;
            }
        }

        private void titleBar_RefreshButtonClicked(object sender, RoutedEventArgs e)
        {
            GetAppDetail();
            _ = scrollViewer.ChangeView(null, 0, null);
        }

        private void titleBar_BackButtonClicked(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void titleBar_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
    internal class AppModel : Entity
    {
        public string DownloadUrl { get; private set; }
        public string Url { get; private set; }
        public double Score { get; private set; }
        public string Vote { get; private set; }
        public string Title { get; private set; }
        public string EntityID { get; private set; }
        public string Version { get; private set; }
        public string Apksize { get; private set; }
        public string HotNum { get; private set; }
        public string VoteCount { get; private set; }
        public string FollowNum { get; private set; }
        public string CommentNum { get; private set; }
        public string DownloadNum { get; private set; }
        public string ChangeLog { get; private set; }
        public string Introduce { get; private set; }
        public string Description { get; private set; }
        public string PubDate { get; private set; }
        public string LastUpdate { get; private set; }
        public string PackageName { get; private set; }
        public string ApkRomVersion { get; private set; }
        public string DeveloperName { get; private set; }
        public string DeveloperUrl { get; private set; }
        public bool ShowPicArr { get; private set; }
        public ImageViewModel Logo { get; private set; }
        public ImageViewModel DeveloperAvatar { get; private set; }
        public List<string> Pics { get; private set; } = new List<string>();
        public ObservableCollection<ImageData> PicArr { get; private set; }

        public AppModel(JsonObject json) : base(json)
        {
            if (json.TryGetValue("dataRow", out IJsonValue v1) && !string.IsNullOrEmpty(v1.ToString()))
            {
                JsonObject dataRow = v1.GetObject();
                if (dataRow.TryGetValue("logo", out IJsonValue logo) && !string.IsNullOrEmpty(logo.GetString()))
                {
                    Logo = new ImageViewModel(logo.GetString(), ImageType.Icon);
                }
                if (dataRow.TryGetValue("apkfile", out IJsonValue apkfile) && apkfile.GetString().IndexOf("http") == 0)
                {
                    DownloadUrl = SettingsHelper.IsSpecialUser ? apkfile.GetString() : string.Empty;
                }
                else if (json.TryGetValue("apkDetailDownloadUrl", out IJsonValue apkDetailDownloadUrl) && !string.IsNullOrEmpty(apkDetailDownloadUrl.GetString()))
                {
                    DownloadUrl = SettingsHelper.IsSpecialUser ? apkDetailDownloadUrl.GetString() : string.Empty;
                }
                if (dataRow.TryGetValue("id", out IJsonValue id) && !string.IsNullOrEmpty(id.GetNumber().ToString()))
                {
                    EntityID = id.GetNumber().ToString();
                }
                if (dataRow.TryGetValue("title", out IJsonValue title) && !string.IsNullOrEmpty(title.GetString()))
                {
                    Title = title.GetString();
                }
                else if (dataRow.TryGetValue("shorttitle", out IJsonValue shorttitle) && !string.IsNullOrEmpty(shorttitle.GetString()))
                {
                    Title = shorttitle.GetString();
                }
                if (dataRow.TryGetValue("score_v10", out IJsonValue score_v10) && !string.IsNullOrEmpty(score_v10.GetNumber().ToString()))
                {
                    Vote = score_v10.GetNumber().ToString();
                }
                if (dataRow.TryGetValue("votescore_v10", out IJsonValue votescore_v10) && !string.IsNullOrEmpty(votescore_v10.GetNumber().ToString()))
                {
                    Score = votescore_v10.GetNumber() / 2;
                }
                if (dataRow.TryGetValue("version", out IJsonValue version) && !string.IsNullOrEmpty(version.GetString()))
                {
                    Version = version.GetString();
                }
                else if (dataRow.TryGetValue("apkversionname", out IJsonValue apkversionname) && !string.IsNullOrEmpty(apkversionname.GetString()))
                {
                    Version = apkversionname.GetString();
                }
                else if (dataRow.TryGetValue("apkversioncode", out IJsonValue apkversioncode) && !string.IsNullOrEmpty(apkversioncode.GetString()))
                {
                    Version = apkversioncode.GetString();
                }
                if (dataRow.TryGetValue("apklength", out IJsonValue apklength) && !string.IsNullOrEmpty(apklength.GetNumber().ToString()))
                {
                    Apksize = UIHelper.GetSizeString(apklength.GetNumber());
                }
                else if (dataRow.TryGetValue("apksize", out IJsonValue apksize) && !string.IsNullOrEmpty(apksize.GetString()))
                {
                    Apksize = apksize.GetString();
                }
                if (dataRow.TryGetValue("changelog", out IJsonValue changelog) && !string.IsNullOrEmpty(changelog.GetString()))
                {
                    ChangeLog = changelog.GetString();
                }
                if (dataRow.TryGetValue("introduce", out IJsonValue introduce) && !string.IsNullOrEmpty(introduce.GetString()))
                {
                    Introduce = introduce.GetString();
                    Introduce = UIHelper.CSStoMarkDown(Introduce);
                }
                if (dataRow.TryGetValue("description", out IJsonValue description) && !string.IsNullOrEmpty(description.GetString()))
                {
                    Description = description.GetString();
                }
                else if (dataRow.TryGetValue("subtitle", out IJsonValue subtitle) && !string.IsNullOrEmpty(subtitle.GetString()))
                {
                    Description = subtitle.GetString();
                }
                else if (dataRow.TryGetValue("remark", out IJsonValue remark) && !string.IsNullOrEmpty(remark.GetString()))
                {
                    Description = remark.GetString();
                    Description = UIHelper.ReplaceHtml(Description);
                }
                if (dataRow.TryGetValue("hot_num", out IJsonValue hot_num) && !string.IsNullOrEmpty(hot_num.GetNumber().ToString()))
                {
                    HotNum = UIHelper.GetNumString(hot_num.GetNumber());
                }
                if (dataRow.TryGetValue("voteCount", out IJsonValue voteCount) && !string.IsNullOrEmpty(UIHelper.GetValue(voteCount)))
                {
                    VoteCount = UIHelper.GetValue(voteCount);
                }
                if (dataRow.TryGetValue("followCount", out IJsonValue followCount) && !string.IsNullOrEmpty(UIHelper.GetValue(followCount)))
                {
                    FollowNum = UIHelper.GetValue(followCount);
                }
                if (dataRow.TryGetValue("commentCount", out IJsonValue commentCount) && !string.IsNullOrEmpty(UIHelper.GetValue(commentCount)))
                {
                    CommentNum = UIHelper.GetValue(commentCount);
                }
                if (dataRow.TryGetValue("downCount", out IJsonValue downCount) && !string.IsNullOrEmpty(UIHelper.GetValue(downCount)))
                {
                    DownloadNum = UIHelper.GetValue(downCount);
                }
                if (dataRow.TryGetValue("pubdate", out IJsonValue pubdate) && !string.IsNullOrEmpty(pubdate.GetNumber().ToString()))
                {
                    PubDate = UIHelper.ConvertTime(pubdate.GetNumber());
                }
                if (dataRow.TryGetValue("lastupdate", out IJsonValue lastupdate) && !string.IsNullOrEmpty(lastupdate.GetNumber().ToString()))
                {
                    LastUpdate = UIHelper.ConvertTime(lastupdate.GetNumber());
                }
                if (dataRow.TryGetValue("apkRomVersion", out IJsonValue apkRomVersion) && !string.IsNullOrEmpty(apkRomVersion.GetString()))
                {
                    ApkRomVersion = "Android " + apkRomVersion.GetString();
                }
                if (dataRow.TryGetValue("packageName", out IJsonValue packageName) && !string.IsNullOrEmpty(packageName.GetString()))
                {
                    PackageName = packageName.GetString();
                }
                if (ChangeLog == null)
                {
                    ChangeLog = Description;
                    Description = null;
                }
                ShowPicArr = dataRow.TryGetValue("screenList", out IJsonValue screenList) && screenList.GetArray().Count > 0 && screenList != null;
                if (ShowPicArr)
                {
                    PicArr = new ObservableCollection<ImageData>();
                    GetPic(screenList);
                }
                if (dataRow.TryGetValue("developerProfile", out IJsonValue v2) && !string.IsNullOrEmpty(v2.GetObject().ToString()))
                {
                    JsonObject developerProfile = v2.GetObject();
                    if (developerProfile.TryGetValue("username", out IJsonValue username) && !string.IsNullOrEmpty(username.GetString()))
                    {
                        DeveloperName = username.GetString();
                    }
                    if (developerProfile.TryGetValue("userAvatar", out IJsonValue userAvatar) && !string.IsNullOrEmpty(userAvatar.GetString()))
                    {
                        DeveloperAvatar = new ImageViewModel(userAvatar.GetString(), ImageType.BigAvatar);
                    }
                    if (developerProfile.TryGetValue("uid", out IJsonValue uid) && !string.IsNullOrEmpty(uid.GetNumber().ToString()))
                    {
                        DeveloperUrl = "/u/" + uid.GetNumber().ToString();
                    }
                }
                if (string.IsNullOrEmpty(DeveloperName) || DeveloperAvatar == null)
                {
                    if (dataRow.TryGetValue("developername", out IJsonValue developername) && !string.IsNullOrEmpty(developername.GetString()))
                    {
                        DeveloperName = developername.GetString();
                    }
                    else if (dataRow.TryGetValue("updatername", out IJsonValue updatername) && !string.IsNullOrEmpty(updatername.GetString()))
                    {
                        DeveloperName = updatername.GetString();
                    }
                    else if (dataRow.TryGetValue("creatorname", out IJsonValue creatorname) && !string.IsNullOrEmpty(creatorname.GetString()))
                    {
                        DeveloperName = creatorname.GetString();
                    }
                    if (dataRow.TryGetValue("developerurl", out IJsonValue developerurl) && !string.IsNullOrEmpty(developerurl.GetString()))
                    {
                        DeveloperUrl = developerurl.GetString();
                    }
                    else if (dataRow.TryGetValue("developeruid", out IJsonValue developeruid) && !string.IsNullOrEmpty(developeruid.GetNumber().ToString()) && developeruid.GetNumber() != 0)
                    {
                        DeveloperUrl = "/u/" + developeruid.GetNumber().ToString();
                    }
                    else if (dataRow.TryGetValue("updateruid", out IJsonValue updateruid) && !string.IsNullOrEmpty(updateruid.GetNumber().ToString()) && updateruid.GetNumber() != 0)
                    {
                        DeveloperUrl = "/u/" + updateruid.GetNumber().ToString();
                    }
                    else if (dataRow.TryGetValue("creatoruid", out IJsonValue creatoruid) && !string.IsNullOrEmpty(creatoruid.GetNumber().ToString()) && creatoruid.GetNumber() != 0)
                    {
                        DeveloperUrl = "/u/" + creatoruid.GetNumber().ToString();
                    }
                    if (DeveloperAvatar == null)
                    {
                        DeveloperAvatar = Logo;
                    }
                }
            }
        }
        private async void GetPic(IJsonValue screenList)
        {
            foreach (IJsonValue item in screenList.GetArray())
            {
                Pics.Add(item.GetString());
                PicArr.Add(new ImageData { Pic = await ImageCache.GetImage(ImageType.OriginImage, item.GetString()), url = item.GetString() });
            }
        }
    }
}
