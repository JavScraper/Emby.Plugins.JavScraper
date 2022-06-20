using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.JavScraper.Services
{
    /// <summary>
    /// 百度人体分析
    /// https://ai.baidu.com/ai-doc/BODY/0k3cpyxme
    /// </summary>
    public class BodyAnalysisService : BaiduServiceBase
    {
        public BodyAnalysisService()
            : this(string.Empty, string.Empty)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="secretKey"></param>
        private BodyAnalysisService(string apiKey, string secretKey)
            : base(apiKey, secretKey)
        {
        }

        /// <summary>
        /// 获取人头中间的X坐标
        /// </summary>
        /// <param name="imageBytes"></param>
        /// <returns></returns>
        public Task<BaiduBodyAnalysisResult?> BodyAnalysis(byte[] imageBytes)
        {
            try
            {
                var image = Convert.ToBase64String(imageBytes);
                return DoPostForm<BaiduBodyAnalysisResult>("https://aip.baidubce.com/rest/2.0/image-classify/v1/body_analysis", new Dictionary<string, string>() { ["image"] = image });
            }
            catch
            {
            }

            return Task.FromResult<BaiduBodyAnalysisResult?>(null);
        }

        public class BaiduBodyAnalysisResult
        {
            [JsonPropertyName("person_num")]
            public int PersonNum { get; set; }

            [JsonPropertyName("person_info")]
            public IReadOnlyCollection<BaiduBodyAnalysisPersonInfo>? PersonInfos { get; set; }

            [JsonPropertyName("log_id")]
            public string? LogId { get; set; }
        }

        public class BaiduBodyAnalysisPersonInfo
        {
            [JsonPropertyName("body_parts")]
            public BaiduBodyAnalysisBodyParts BodyParts { get; set; } = new();

            [JsonPropertyName("location")]
            public BaiduBodyAnalysisBodyLocation Location { get; set; } = new();
        }

        public class BaiduBodyAnalysisBodyParts
        {
            [JsonPropertyName("left_hip")]
            public BaiduBodyAnalysisBodyPoint? LeftHip { get; set; }

            [JsonPropertyName("top_head")]
            public BaiduBodyAnalysisBodyPoint? TopHead { get; set; }

            [JsonPropertyName("right_mouth_corner")]
            public BaiduBodyAnalysisBodyPoint? RightMouthCorner { get; set; }

            [JsonPropertyName("neck")]
            public BaiduBodyAnalysisBodyPoint? Neck { get; set; }

            [JsonPropertyName("left_shoulder")]
            public BaiduBodyAnalysisBodyPoint? LeftShoulder { get; set; }

            [JsonPropertyName("left_knee")]
            public BaiduBodyAnalysisBodyPoint? LeftKnee { get; set; }

            [JsonPropertyName("left_ankle")]
            public BaiduBodyAnalysisBodyPoint? LeftAnkle { get; set; }

            [JsonPropertyName("left_mouth_corner")]
            public BaiduBodyAnalysisBodyPoint? LeftMouthCorner { get; set; }

            [JsonPropertyName("right_elbow")]
            public BaiduBodyAnalysisBodyPoint? RightElbow { get; set; }

            [JsonPropertyName("right_ear")]
            public BaiduBodyAnalysisBodyPoint? RightEar { get; set; }

            [JsonPropertyName("nose")]
            public BaiduBodyAnalysisBodyPoint? Nose { get; set; }

            [JsonPropertyName("left_eye")]
            public BaiduBodyAnalysisBodyPoint? LeftEye { get; set; }

            [JsonPropertyName("right_eye")]
            public BaiduBodyAnalysisBodyPoint? RightEye { get; set; }

            [JsonPropertyName("right_hip")]
            public BaiduBodyAnalysisBodyPoint? RightHip { get; set; }

            [JsonPropertyName("left_wrist")]
            public BaiduBodyAnalysisBodyPoint? LeftWrist { get; set; }

            [JsonPropertyName("left_ear")]
            public BaiduBodyAnalysisBodyPoint? LeftEar { get; set; }

            [JsonPropertyName("left_elbow")]
            public BaiduBodyAnalysisBodyPoint? LeftElbow { get; set; }

            [JsonPropertyName("right_shoulder")]
            public BaiduBodyAnalysisBodyPoint? RightShoulder { get; set; }

            [JsonPropertyName("right_ankle")]
            public BaiduBodyAnalysisBodyPoint? RightAnkle { get; set; }

            [JsonPropertyName("right_knee")]
            public BaiduBodyAnalysisBodyPoint? RightKnee { get; set; }

            [JsonPropertyName("right_wrist")]
            public BaiduBodyAnalysisBodyPoint? RightWrist { get; set; }
        }

        public class BaiduBodyAnalysisBodyPoint
        {
            [JsonPropertyName("y")]
            public float Y { get; set; }

            [JsonPropertyName("x")]
            public float X { get; set; }

            [JsonPropertyName("score")]
            public float Score { get; set; }
        }

        public class BaiduBodyAnalysisBodyLocation
        {
            [JsonPropertyName("height")]
            public float Height { get; set; }

            [JsonPropertyName("width")]
            public float Width { get; set; }

            [JsonPropertyName("top")]
            public float Top { get; set; }

            [JsonPropertyName("score")]
            public float Score { get; set; }

            [JsonPropertyName("left")]
            public float Left { get; set; }
        }
    }
}
