using Baidu.AI;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Baidu
{
    /// <summary>
    /// 百度人体分析
    /// https://ai.baidu.com/ai-doc/BODY/0k3cpyxme
    /// </summary>
    public class BodyAnalysisService : BaiduServiceBase
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="secretKey"></param>
        /// <param name="jsonSerializer"></param>
        public BodyAnalysisService(string apiKey, string secretKey, IJsonSerializer jsonSerializer)
            : base(apiKey, secretKey, jsonSerializer)
        {

        }

        /// <summary>
        /// 获取人头中间的X坐标
        /// </summary>
        /// <param name="image_bytes"></param>
        /// <returns></returns>
        public Task<BaiduBodyAnalysisResult> BodyAnalysis(byte[] image_bytes)
        {
            try
            {
                var image = Convert.ToBase64String(image_bytes);
                return DoPostForm<BaiduBodyAnalysisResult>("https://aip.baidubce.com/rest/2.0/image-classify/v1/body_analysis", new Dictionary<string, string>() { ["image"] = image });
            }
            catch { }

            return Task.FromResult<BaiduBodyAnalysisResult>(null);
        }
    }

    public class BaiduBodyAnalysisResult
    {
        public int person_num { get; set; }
        public BaiduBodyAnalysisPersonInfo[] person_info { get; set; }
        public string log_id { get; set; }
    }

    public class BaiduBodyAnalysisPersonInfo
    {
        public BaiduBodyAnalysisBodyParts body_parts { get; set; }
        public BaiduBodyAnalysisBodyLocation location { get; set; }
    }

    public class BaiduBodyAnalysisBodyParts
    {
        public BaiduBodyAnalysisBodyPoint left_hip { get; set; }
        public BaiduBodyAnalysisBodyPoint top_head { get; set; }
        public BaiduBodyAnalysisBodyPoint right_mouth_corner { get; set; }
        public BaiduBodyAnalysisBodyPoint neck { get; set; }
        public BaiduBodyAnalysisBodyPoint left_shoulder { get; set; }
        public BaiduBodyAnalysisBodyPoint left_knee { get; set; }
        public BaiduBodyAnalysisBodyPoint left_ankle { get; set; }
        public BaiduBodyAnalysisBodyPoint left_mouth_corner { get; set; }
        public BaiduBodyAnalysisBodyPoint right_elbow { get; set; }
        public BaiduBodyAnalysisBodyPoint right_ear { get; set; }
        public BaiduBodyAnalysisBodyPoint nose { get; set; }
        public BaiduBodyAnalysisBodyPoint left_eye { get; set; }
        public BaiduBodyAnalysisBodyPoint right_eye { get; set; }
        public BaiduBodyAnalysisBodyPoint right_hip { get; set; }
        public BaiduBodyAnalysisBodyPoint left_wrist { get; set; }
        public BaiduBodyAnalysisBodyPoint left_ear { get; set; }
        public BaiduBodyAnalysisBodyPoint left_elbow { get; set; }
        public BaiduBodyAnalysisBodyPoint right_shoulder { get; set; }
        public BaiduBodyAnalysisBodyPoint right_ankle { get; set; }
        public BaiduBodyAnalysisBodyPoint right_knee { get; set; }
        public BaiduBodyAnalysisBodyPoint right_wrist { get; set; }
    }

    public class BaiduBodyAnalysisBodyPoint
    {
        public float y { get; set; }
        public float x { get; set; }
        public float score { get; set; }
    }

    public class BaiduBodyAnalysisBodyLocation
    {
        public float height { get; set; }
        public float width { get; set; }
        public float top { get; set; }
        public float score { get; set; }
        public float left { get; set; }
    }
}