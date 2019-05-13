using RestSharp;


namespace GenericPOSRestService.RESTListener
{
    public class RequestDetails
    {
        public void HeaderInformation(out RestClient client, out RestRequest request)

        {

            client = new RestClient(RESTNancyModule.OrderUrl);
            request = new RestRequest(Method.POST);

            //header items
            request.AddHeader("cache-control", RESTNancyModule.CacheType);
            request.AddHeader("x-auth-brandtoken", RESTNancyModule.BrandToken);
            request.AddHeader("Accept", RESTNancyModule.AcceptType);
            request.AddHeader("Content-Type", RESTNancyModule.ContentType);

        }
    }
}
