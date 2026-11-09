using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LayoutManagementWebApp.Models;

namespace LayoutManagementWebApp.Services;

public class RealEstateApiClient
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IHttpClientFactory _factory;
    private readonly AuthSession _session;
    private readonly JwtAuthStateProvider _auth;

    public RealEstateApiClient(IHttpClientFactory factory, AuthSession session, JwtAuthStateProvider auth)
    {
        _factory = factory;
        _session = session;
        _auth = auth;
    }

    private HttpClient Http
    {
        get
        {
            var client = _factory.CreateClient("RealEstate");
            if (!string.IsNullOrWhiteSpace(_session.Token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
            }

            return client;
        }
    }

    public Task<DashboardModel?> GetDashboardAsync() =>
        GetAsync<DashboardModel>("api/dashboard");

    public Task<List<PropertyModel>?> GetPropertiesAsync() =>
        GetAsync<List<PropertyModel>>("api/properties");

    public async Task SavePropertyAsync(PropertyModel model)
    {
        var response = model.Id == 0
            ? await Http.PostAsJsonAsync("api/properties", model, JsonOptions)
            : await Http.PutAsJsonAsync($"api/properties/{model.Id}", model, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task DeletePropertyAsync(int id) =>
        await EnsureSuccessAsync(await Http.DeleteAsync($"api/properties/{id}"));

    public Task<List<AgentModel>?> GetAgentsAsync() =>
        GetAsync<List<AgentModel>>("api/agents");

    public async Task SaveAgentAsync(AgentModel model)
    {
        var response = model.Id == 0
            ? await Http.PostAsJsonAsync("api/agents", model, JsonOptions)
            : await Http.PutAsJsonAsync($"api/agents/{model.Id}", model, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task DeleteAgentAsync(int id) =>
        await EnsureSuccessAsync(await Http.DeleteAsync($"api/agents/{id}"));

    public Task<List<ClientModel>?> GetClientsAsync() =>
        GetAsync<List<ClientModel>>("api/clients");

    public async Task SaveClientAsync(ClientModel model)
    {
        var response = model.Id == 0
            ? await Http.PostAsJsonAsync("api/clients", model, JsonOptions)
            : await Http.PutAsJsonAsync($"api/clients/{model.Id}", model, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task DeleteClientAsync(int id) =>
        await EnsureSuccessAsync(await Http.DeleteAsync($"api/clients/{id}"));

    public Task<List<InquiryModel>?> GetInquiriesAsync() =>
        GetAsync<List<InquiryModel>>("api/inquiries");

    public async Task SaveInquiryAsync(InquiryModel model)
    {
        var response = model.Id == 0
            ? await Http.PostAsJsonAsync("api/inquiries", model, JsonOptions)
            : await Http.PutAsJsonAsync($"api/inquiries/{model.Id}", model, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task DeleteInquiryAsync(int id) =>
        await EnsureSuccessAsync(await Http.DeleteAsync($"api/inquiries/{id}"));

    public async Task<AuthResponse?> LoginAsync(LoginModel model)
    {
        var response = await Http.PostAsJsonAsync("api/auth/login", model, JsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterModel model)
    {
        var response = await Http.PostAsJsonAsync("api/auth/register", model, JsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
    }

    public Task<List<LayoutSummaryModel>?> GetLayoutsAsync() =>
        GetAsync<List<LayoutSummaryModel>>("api/layouts");

    public Task<LayoutDetailModel?> GetLayoutAsync(int id) =>
        GetAsync<LayoutDetailModel>($"api/layouts/{id}");

    public async Task<LayoutDetailModel?> ImportSketchAsync(Stream stream, string fileName, string layoutName, string mode = "extract", IReadOnlyList<ManualPlotInputModel>? plots = null)
    {
        using var content = new MultipartFormDataContent();
        var file = new StreamContent(stream);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var mediaType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/json"
        };
        file.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        content.Add(file, "file", fileName);
        content.Add(new StringContent(layoutName), "name");
        content.Add(new StringContent(mode), "mode");
        if (plots is not null)
        {
            content.Add(new StringContent(JsonSerializer.Serialize(plots, JsonOptions)), "plots");
        }

        var response = await Http.PostAsync("api/layouts/import", content);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LayoutDetailModel>(JsonOptions);
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetSketchImageAsync(int id)
    {
        var response = await Http.GetAsync($"api/layouts/{id}/image");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        return (bytes, contentType);
    }

    public async Task<LayoutDetailModel?> ExtractLayoutAsync(int id)
    {
        var response = await Http.PostAsync($"api/layouts/{id}/extract", null);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LayoutDetailModel>(JsonOptions);
    }

    public async Task<LayoutDetailModel?> SaveDocumentAsync(int id, LayoutDocumentModel document)
    {
        var response = await Http.PutAsJsonAsync($"api/layouts/{id}/document", document, JsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LayoutDetailModel>(JsonOptions);
    }

    public async Task<LayoutDetailModel?> CompleteLayoutAsync(int id)
    {
        var response = await Http.PostAsync($"api/layouts/{id}/complete", null);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LayoutDetailModel>(JsonOptions);
    }

    public async Task DeleteLayoutAsync(int id) =>
        await EnsureSuccessAsync(await Http.DeleteAsync($"api/layouts/{id}"));

    public async Task<LayoutDetailModel?> SaveOwnerPayableAsync(int id, decimal ownerPayable)
    {
        var response = await Http.PutAsJsonAsync($"api/layouts/{id}/owner", new { ownerPayable }, JsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LayoutDetailModel>(JsonOptions);
    }

    public async Task<LayoutDetailModel?> AddOwnerPaymentAsync(int id, DateTime paidOn, decimal amount)
    {
        var response = await Http.PostAsJsonAsync($"api/layouts/{id}/owner-payments", new { paidOn, amount }, JsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LayoutDetailModel>(JsonOptions);
    }

    public async Task<LayoutDetailModel?> UpdateOwnerPaymentAsync(int id, OwnerPaymentModel payment)
    {
        var response = await Http.PutAsJsonAsync($"api/layouts/{id}/owner-payments/{payment.Id}", new { payment.PaidOn, payment.Amount }, JsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LayoutDetailModel>(JsonOptions);
    }

    public async Task<LayoutDetailModel?> DeleteOwnerPaymentAsync(int id, int paymentId)
    {
        var response = await Http.DeleteAsync($"api/layouts/{id}/owner-payments/{paymentId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LayoutDetailModel>(JsonOptions);
    }

    public async Task<LayoutDetailModel?> SaveExpensesAsync(int id, decimal surveyorAmount, int tipperCount, decimal tipperCost, decimal miscellaneousAmount)
    {
        var response = await Http.PutAsJsonAsync($"api/layouts/{id}/expenses", new { surveyorAmount, tipperCount, tipperCost, miscellaneousAmount }, JsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LayoutDetailModel>(JsonOptions);
    }

    public async Task<PlotModel?> AddPlotRowAsync(int id)
    {
        var response = await Http.PostAsync($"api/layouts/{id}/plots", null);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PlotModel>(JsonOptions);
    }

    public Task<List<RegistrationReminderModel>?> GetNotificationsAsync() =>
        GetAsync<List<RegistrationReminderModel>>("api/notifications");

    public async Task MarkNotificationReadAsync(int id) =>
        await EnsureSuccessAsync(await Http.PostAsync($"api/notifications/{id}/read", null));

    public async Task<PlotModel?> SaveBookingAsync(PlotModel plot)
    {
        var response = await Http.PutAsJsonAsync($"api/plots/{plot.Id}/booking", new
        {
            plot.Area,
            plot.Price,
            plot.AgentId,
            plot.ClientId,
            plot.ClientName,
            plot.ClientMobile,
            plot.AdvanceReceived,
            plot.AdvanceReceivedOn,
            plot.RegistrationOn,
            plot.Comment,
            plot.Status
        }, JsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PlotModel>(JsonOptions);
    }

    public async Task UploadPlotSurveyAsync(int plotId, Stream stream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue(fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg");
        content.Add(file, "file", fileName);
        await EnsureSuccessAsync(await Http.PostAsync($"api/plots/{plotId}/survey", content));
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetPlotSurveyAsync(int plotId)
    {
        var response = await Http.GetAsync($"api/plots/{plotId}/survey");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        return (bytes, contentType);
    }

    public async Task DeletePlotSurveyAsync(int plotId) =>
        await EnsureSuccessAsync(await Http.DeleteAsync($"api/plots/{plotId}/survey"));

    public Task<List<UserModel>?> GetUsersAsync() =>
        GetAsync<List<UserModel>>("api/users");

    public Task<List<LayoutCatalogItemModel>?> GetLayoutCatalogAsync() =>
        GetAsync<List<LayoutCatalogItemModel>>("api/layouts/catalog");

    public async Task<UserModel?> CreateUserAsync(string fullName, string username, string email, string password, IReadOnlyList<UserSketchGrantModel> sketches)
    {
        var response = await Http.PostAsJsonAsync("api/users", new { fullName, username, email, password, sketches }, JsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<UserModel>(JsonOptions);
    }

    public Task<List<UserSketchGrantModel>?> GetUserSketchesAsync(string id) =>
        GetAsync<List<UserSketchGrantModel>>($"api/users/{id}/sketches");

    public async Task SaveUserSketchesAsync(string id, IReadOnlyList<UserSketchGrantModel> sketches)
    {
        var response = await Http.PutAsJsonAsync($"api/users/{id}/sketches", new { sketches }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public Task<List<UserModel>?> GetPartnerUsersAsync(int layoutId) =>
        GetAsync<List<UserModel>>($"api/layouts/{layoutId}/partner-users");

    public Task<List<LayoutPartnerModel>?> GetPartnersAsync(int layoutId) =>
        GetAsync<List<LayoutPartnerModel>>($"api/layouts/{layoutId}/partners");

    public async Task<List<LayoutPartnerModel>?> AddPartnerAsync(int layoutId, string userId, string level)
    {
        var response = await Http.PostAsJsonAsync($"api/layouts/{layoutId}/partners", new { userId, level }, JsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<LayoutPartnerModel>>(JsonOptions);
    }

    public async Task<List<LayoutPartnerModel>?> RemovePartnerAsync(int layoutId, string userId)
    {
        var response = await Http.DeleteAsync($"api/layouts/{layoutId}/partners/{userId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<LayoutPartnerModel>>(JsonOptions);
    }

    public async Task SetCredentialsAsync(string id, string username, string? newPassword)
    {
        var response = await Http.PutAsJsonAsync($"api/users/{id}/credentials", new { username, newPassword }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task SetRoleAsync(string id, string role)
    {
        var response = await Http.PutAsJsonAsync($"api/users/{id}/role", new { role }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task SetLoginAccessAsync(string id, bool allowed)
    {
        var response = await Http.PutAsJsonAsync($"api/users/{id}/login", new { allowed }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    private async Task<T?> GetAsync<T>(string url)
    {
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // A 401 on a request we sent a token for means that token is no longer valid
        // (expired, or its account was removed). Clear the session so the UI returns to the
        // login page instead of surfacing a raw "401" on every action.
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && !string.IsNullOrWhiteSpace(_session.Token))
        {
            await _auth.MarkSessionExpiredAsync();
            throw new InvalidOperationException("Your session has ended or your account is no longer valid. Please sign in again.");
        }

        var body = await response.Content.ReadAsStringAsync();
        var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body;
        throw new InvalidOperationException($"{(int)response.StatusCode}: {detail}");
    }
}
