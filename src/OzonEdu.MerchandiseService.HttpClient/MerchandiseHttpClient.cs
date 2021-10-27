﻿using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OzonEdu.MerchandiseService.HttpModels;

namespace OzonEdu.MerchandiseService.HttpClient
{
    public class MerchandiseHttpClient
    {
        private readonly System.Net.Http.HttpClient _httpClient;

        public MerchandiseHttpClient(System.Net.Http.HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<MerchOrderResponse> CreateMerchOrder(MerchOrderPostViewModel model, CancellationToken token)
        {
            var response = await _httpClient.PostAsJsonAsync("v1/api/merch", model, token);
            var body = await response.Content.ReadAsStringAsync(token);
            return JsonSerializer.Deserialize<MerchOrderResponse>(body);
        }
        
        public async Task<List<MerchItemResponse>> GetMerchListByEmployeeId(int employeeId, CancellationToken token)
        {
            var response = await _httpClient.GetAsync($"v1/api/merch/{employeeId.ToString()}", token);
            var body = await response.Content.ReadAsStringAsync(token);
            return JsonSerializer.Deserialize<List<MerchItemResponse>>(body); 
        }
    }
}