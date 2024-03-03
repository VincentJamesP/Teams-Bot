using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using KTI.PAL.Teams.Core.Services;


namespace KTI.PAL.Teams.Core.Domains.Merlot
{
    public interface IMerlotEmployee
    {
        /// <summary>
        /// Get employee information from Merlot.
        /// </summary>
        /// <param name="code">The empCode of the employee to get.</param>
        /// <returns>An awaitable Task containing employee information.</returns>
        Task<Core.Models.Merlot.Employee> Get(string code);
    }

    public class Employees : IMerlotEmployee
    {
        private readonly ILogger<Employees> _logger;
        private readonly IMerlotService _service;

        public Employees() { }

        public Employees(ILogger<Employees> logger, IMerlotService service)
        {
            _logger = logger;
            _service = service;
        }

        public async Task<Core.Models.Merlot.Employee> Get(string code)
        {
            var response = await _service.AuthenticatedApiCall($"/employee/api/employee/{code}", "GET", null, null);

            if (response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<Core.Models.Merlot.Employee>(await response.Content.ReadAsStringAsync());
            }

            return null;
        }
    }
}
