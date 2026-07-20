using System.Net;

namespace StarkAgroAPI.Tests.Helpers
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<Uri?> RequestedUris { get; } = new();

        /// <summary>Corpo de cada request enviado — permite asserir o payload, não só a URL.</summary>
        public List<string> RequestBodies { get; } = new();

        public void EnqueueResponse(HttpResponseMessage response)
        {
            _responses.Enqueue(response);
        }

        public void EnqueueResponse(HttpStatusCode statusCode, string content)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri);

            if (request.Content is not null)
                RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));

            if (_responses.Count == 0)
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);

            return _responses.Dequeue();
        }
    }
}
