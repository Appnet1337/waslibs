﻿using System;
using System.Collections.Generic;
using Windows.Web.Http;
using System.Threading.Tasks;
using AppStudio.DataProviders.Core;
using AppStudio.DataProviders.Exceptions;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Linq;

namespace AppStudio.DataProviders.Facebook
{
    public class FacebookDataProvider : DataProviderBase<FacebookDataConfig, FacebookSchema>
    {
        private const string BaseUrl = @"https://graph.facebook.com/v2.5";
        private FacebookOAuthTokens _tokens;

        public string ContinuationToken { get; set; }

        public override bool HasMoreItems
        {
            get
            {
                return !string.IsNullOrEmpty(ContinuationToken);
            }
        }

        public FacebookDataProvider(FacebookOAuthTokens tokens)
        {
            _tokens = tokens;
        }

        protected override async Task<IEnumerable<TSchema>> GetDataAsync<TSchema>(FacebookDataConfig config, int maxRecords, IParser<TSchema> parser)
        {
            var settings = new HttpRequestSettings
            {
                RequestedUri = GetUri(config, maxRecords)
            };

            return await GetDataInternal(parser, settings);
        }

        protected override async Task<IEnumerable<TSchema>> GetMoreDataAsync<TSchema>(FacebookDataConfig config, int maxRecords, IParser<TSchema> parser)
        {
            var settings = new HttpRequestSettings
            {
                RequestedUri = GetContinuationUri()
            };

            return await GetDataInternal(parser, settings);
        } 

        protected override IParser<FacebookSchema> GetDefaultParserInternal(FacebookDataConfig config)
        {
            return new FacebookParser();
        }

        protected override void ValidateConfig(FacebookDataConfig config)
        {
            if (config.UserId == null)
            {
                throw new ConfigParameterNullException("UserId");
            }
            if (_tokens == null)
            {
                throw new ConfigParameterNullException("Tokens");
            }
            if (string.IsNullOrEmpty(_tokens.AppId))
            {
                throw new OAuthKeysNotPresentException("AppId");
            }
            if (string.IsNullOrEmpty(_tokens.AppSecret))
            {
                throw new OAuthKeysNotPresentException("AppSecret");
            }
        }       

        private Uri GetUri(FacebookDataConfig config, int maxRecords)
        {
            return new Uri($"{BaseUrl}/{config.UserId}/posts?&access_token={_tokens.AppId}|{ _tokens.AppSecret}&fields=id,message,from,created_time,link,full_picture&limit={maxRecords}", UriKind.Absolute);
        }

        private Uri GetContinuationUri()
        {
            return new Uri(ContinuationToken);
        }

        private string GetContinuationToken(string data)
        {
            var facebookResponse = JsonConvert.DeserializeObject<FacebookGraphResponse>(data);
            return facebookResponse?.paging?.next;
        }

        private async Task<IEnumerable<TSchema>> GetDataInternal<TSchema>(IParser<TSchema> parser, HttpRequestSettings settings) where TSchema : SchemaBase
        {
            HttpRequestResult result = await HttpRequest.DownloadAsync(settings);

            if (result.Success)
            {
                ContinuationToken = GetContinuationToken(result.Result);
                return parser.Parse(result.Result);
            }

            if (result.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new OAuthKeysRevokedException($"Request failed with status code {(int)HttpStatusCode.BadRequest} and reason '{result.Result}'");
            }

            throw new RequestFailedException(result.StatusCode, result.Result);
        }
    }
}
