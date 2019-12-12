using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using ApplicationCore.Interfaces;
using ApplicationCore.DTOs;
using ApplicationCore.Entities;
using ApplicationCore.Services;
using Newtonsoft.Json;
using System.IO;
using Presentation.Services.ServiceInterfaces;
using Presentation.ViewModels;
using ApplicationCore;

namespace Presentation.Pages.Admin
{
    public class FlightModel : PageModel
    {
        private readonly IUnitOfWork _unitofwork;
        private readonly IFlightService _services;
        private readonly IPlaneService _planeServices;
        private readonly IRouteService _routeServices;
        private readonly IFlightVMService _serviceVM;

        public FlightModel(IFlightService services, IFlightVMService serviceVM, IUnitOfWork unitofwork, IPlaneService servicesPlane, IRouteService routeServices)
        {
            _unitofwork = unitofwork;
            _services = services;
            _planeServices = servicesPlane;
            _routeServices = routeServices;
            _serviceVM = serviceVM;
        }

        public FlightPageVM ListFlightPage { get; set; }
        public FlightPageVM ListFlightPage_2 { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchString { get; set; }

        public IEnumerable<FlightDTO> ListFlights { get; set; }
        public IEnumerable<FlightDTO> ListFlights_2 { get; set; }
        public IList<string> ListNamePlanes { get; set; }
        public IEnumerable<RouteDTO> ListRoutes { get; set; }
        public IList<string> ListNameRoutes { get; set; }
        public IEnumerable<Airport> ListAirports { get; set; }
        public IEnumerable<FlightDetail> ListFlightDetail { get; set; }
        public IList<FlightDetailVM> ListFlightDetailVM { get; set; }
        public async Task OnGet(string searchString, int pageIndex = 1)
        {
            ListFlights = await _services.getAllFlightAsync();
            DateTime depDate = new DateTime(10, 11, 12);
            DateTime arrDate = new DateTime(10, 11, 12);
            ListFlightPage = await _serviceVM.GetFlightPageViewModelAsync(ListFlights, depDate, arrDate, searchString, pageIndex);

            ListAirports = await _unitofwork.Airports.GetAllAsync();
            ListRoutes = await _routeServices.getAllRouteAsync();
            ListNameRoutes = new List<string>();
            foreach (var item in ListRoutes)
            {
                Console.WriteLine(item.RouteId + ": " + item.Origin + " - " + item.Destination);
                ListNameRoutes.Add(item.RouteId + ": " + item.Origin + " - " + item.Destination);
            }
            var ListPlanes = await _planeServices.getAllPlaneAsync();
            ListNamePlanes = new List<string>();
            foreach (var item in ListPlanes)
            {
                var s = await _planeServices.getPlaneFullname(item.PlaneId);
                ListNamePlanes.Add(s);
            }
        }
        public async Task<JsonResult> OnGetSearchFlight(string searchString, int pageIndex = 1)
        {
            await this.OnGet(searchString, pageIndex);
            return new JsonResult(searchString);
        }
        public async Task<JsonResult> OnGetDetailFlight(string id)
        {
            var Flight = await _services.getFlightAsync(id);
            var ListFlightDetailId = await _services.getAllFlightDetailAsync(Flight.FlightId);
            ListFlightDetailVM = new List<FlightDetailVM>();
            foreach (var item in ListFlightDetailId)
            {
                var route = await _unitofwork.Routes.GetByAsync(item.RouteId);
                var origin = await _unitofwork.Airports.GetByAsync(route.Origin);
                var destination = await _unitofwork.Airports.GetByAsync(route.Destination);
                ListFlightDetailVM.Add(new FlightDetailVM(item, origin, destination));
            }
            Dictionary<string, object> Result = new Dictionary<string, object>();
            Result.Add("flight", Flight);
            Result.Add("listFlight", ListFlightDetailVM);
            return new JsonResult(Result);
        }
        public async Task<IActionResult> OnPostDeleteFlight()
        {
            string FlightId = "";
            MemoryStream stream = new MemoryStream();
            Request.Body.CopyTo(stream);
            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream))
            {
                string requestBody = reader.ReadToEnd();
                if (requestBody.Length > 0)
                {
                    var obj = JsonConvert.DeserializeObject<FlightDTO>(requestBody);
                    if (obj != null)
                    {
                        FlightId = obj.FlightId;
                        Console.WriteLine(FlightId + "  ggggg");
                        await _services.disableFlightAsync(FlightId);
                        // await _services.CompleteAsync();
                    }
                }
            }
            string mes = "Remove flight" + FlightId + " Success!";
            return new JsonResult(mes);
        }
        public async Task<JsonResult> OnGetRouteFull()
        {
            ListRoutes = await _routeServices.getAllRouteAsync();
            return new JsonResult(ListRoutes);
        }
        public async Task<JsonResult> OnGetRoutes(string routeid)
        {
            // ListRoutes = await _routeServices.getAllRouteAsync();
            var n = routeid.Count();
            ListRoutes = await _routeServices.getRouteByOriginAsync(routeid.Substring(n - 3, 3));
            return new JsonResult(ListRoutes);
        }
        public async Task<JsonResult> OnGetDateTimes(string arrdate, string routeid)
        {
            // ListRoutes = await _routeServices.getAllRouteAsync();
            if (arrdate.Count() != 0)
            {
                DateTime Date = DateTime.ParseExact(arrdate, "dd-MM-yyyy hh:mm tt", null);
                FlightTime timeDTO = new FlightTime(0, 30);
                DateTime depDate = await _services.calArrDate(Date, timeDTO);

                var routedto = await _routeServices.getRouteAsync(routeid);
                var Listrou = await _routeServices.getRouteByOriginAsync(routedto.Destination);
                routedto = Listrou.ElementAt(0);
                Route route = new Route();
                _routeServices.convertDtoToEntity(routedto, route);
                DateTime arrDate = await _services.calArrDate(Date, route.FlightTime);

                Dictionary<string, object> Result = new Dictionary<string, object>();
                Result.Add("arrDate", arrDate.ToString("dd-MM-yyyy hh:mm tt"));
                Result.Add("depDate", depDate.ToString("dd-MM-yyyy hh:mm tt"));
                return new JsonResult(Result);
            }
            return new JsonResult("null");
        }
        public async Task<IActionResult> OnGetEditFlight(string id)
        {
            var flight = await _services.getFlightAsync(id);
            var flightdetail = await _services.getAllFlightDetailAsync(id);

            EditFlightVM flightVM = new EditFlightVM();
            flightVM.FlightId = flight.FlightId;
            flightVM.PlaneId = await _planeServices.getPlaneFullname(flight.PlaneId);
            flightVM.Status = flight.Status.ToString();
            int i = 0;
            foreach (var item in flightdetail)
            {
                flightVM.flightDetailId.Add(item.FlightDetailId);
                var route = await _routeServices.getRouteAsync(item.RouteId);
                flightVM.routeId.Add(route.RouteId + " : " + route.Origin + " - " + route.Destination);
                flightVM.depDate.Add(item.DepDate.ToString("dd-MM-yyyy hh:mm tt"));
                flightVM.arrDate.Add(item.ArrDate.ToString("dd-MM-yyyy hh:mm tt"));
                ++i;
            }
            return new JsonResult(flightVM);
        }
        public async Task<IActionResult> OnGetCalArrDate(string routeid, string depDate)
        {
            // Console.WriteLine(routeid + " " + year + " " + month + " " + day);
            DateTime DepDate = DateTime.ParseExact(depDate, "dd-MM-yyyy hh:mm tt", null);
            Console.WriteLine(DepDate);
            var routedto = await _routeServices.getRouteAsync(routeid);
            Route route = new Route();
            _routeServices.convertDtoToEntity(routedto, route);
            DateTime arrDate = await _services.calArrDate(DepDate, route.FlightTime);

            return new JsonResult(arrDate.ToString("dd-MM-yyyy hh:mm tt"));
        }
        public async Task<IActionResult> OnPostCreateFlight()
        {
            await Task.Run(() => true);
            string respone = "True";
            MemoryStream stream = new MemoryStream();
            Request.Body.CopyTo(stream);
            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream))
            {

                string requestBody = reader.ReadToEnd();
                if (requestBody.Length > 0)
                {
                    var obj = JsonConvert.DeserializeObject<CreateFlightVM>(requestBody);
                    if (obj != null)
                    {
                        FlightDTO flight = new FlightDTO();
                        flight.PlaneId = obj.PlaneId;
                        if (obj.Status == "AVAILABLE")
                        {
                            flight.Status = 0;
                        }

                        IList<FlightDetailDTO> detailDTO = new List<FlightDetailDTO>();
                        var code = await _services.generateFlightId();
                        Console.WriteLine(code);
                        int n = obj.routeId.Count();
                        for (var i = 0; i < n; ++i)
                        {
                            DateTime depDate = DateTime.ParseExact(obj.depDate[i], "dd-MM-yyyy hh:mm tt", null);
                            DateTime arrDate = DateTime.ParseExact(obj.arrDate[i], "dd-MM-yyyy hh:mm tt", null);
                            FlightDetailDTO detail = new FlightDetailDTO(null, null, obj.routeId[i], depDate, arrDate);
                            detailDTO.Add(detail);
                        }
                        await _services.addFlightAsync(flight, detailDTO);

                    }
                }
            }
            return new JsonResult(respone);
        }
        public async Task<IActionResult> OnPostEditFlight()
        {
            await Task.Run(() => true);
            string respone = "True";
            MemoryStream stream = new MemoryStream();
            Request.Body.CopyTo(stream);
            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream))
            {

                string requestBody = reader.ReadToEnd();
                if (requestBody.Length > 0)
                {
                    var obj = JsonConvert.DeserializeObject<EditFlightVM>(requestBody);
                    if (obj != null)
                    {
                        FlightDTO flight = new FlightDTO();
                        flight.FlightId = obj.FlightId;
                        flight.PlaneId = obj.PlaneId;
                        if (obj.Status == "AVAILABLE")
                        {
                            flight.Status = STATUS.AVAILABLE;
                        }
                        else if (obj.Status == "DISABLED")
                        {
                            flight.Status = STATUS.DISABLED;
                        }

                        IList<FlightDetailDTO> detailDTO = new List<FlightDetailDTO>();
                        int n = obj.routeId.Count();
                        for (var i = 0; i < n; ++i)
                        {
                            DateTime depDate = DateTime.ParseExact(obj.depDate[i], "dd-MM-yyyy hh:mm tt", null);
                            DateTime arrDate = DateTime.ParseExact(obj.arrDate[i], "dd-MM-yyyy hh:mm tt", null);
                            FlightDetailDTO detail = new FlightDetailDTO(null, null, obj.routeId[i], depDate, arrDate);
                            detailDTO.Add(detail);
                        }
                        await _services.updateFlightAsync(flight);
                        Console.WriteLine(flight.FlightId + " ggggg");
                        await _services.addFlightDetailRangeAsync(detailDTO, flight.FlightId);

                    }
                }
            }
            return new JsonResult(respone);
        }
    }
    public class FlightDetailVM
    {
        public string FlightId { get; set; }
        public string FlightDetailId { get; set; }
        public string RouteId { get; set; }
        public string OriginAirport { get; set; }
        public string OriginCountry { get; set; }
        public string DesAirport { get; set; }
        public string DesCountry { get; set; }
        public string ArrDate { get; set; }
        public string DepDate { get; set; }
        public FlightDetailVM() { }
        public FlightDetailVM(
            string FlightId, string FlightDetailId, string RouteId,
            string OriginAirport, string OriginCountry,
            string DesAirport, string DesCountry,
            string ArrDate, string DepDate
            )
        {
            this.FlightId = FlightId;
            this.FlightDetailId = FlightDetailId;
            this.RouteId = RouteId;
            this.OriginAirport = OriginAirport;
            this.OriginCountry = OriginCountry;
            this.DesAirport = DesAirport;
            this.DesCountry = DesCountry;
            this.ArrDate = ArrDate;
            this.DepDate = DepDate;
        }
        public FlightDetailVM(FlightDetail fd, Airport Origin, Airport Destination)
        {
            this.FlightId = fd.FlightId;
            this.FlightDetailId = fd.FlightDetailId;
            this.RouteId = fd.RouteId;
            this.OriginAirport = Origin.AirportName;
            this.OriginCountry = Origin.Address.Country;
            this.DesAirport = Destination.AirportName;
            this.DesCountry = Destination.Address.Country;
            this.ArrDate = fd.ArrDate.ToString();
            this.DepDate = fd.DepDate.ToString();
        }
        public FlightDetailVM(FlightDetailDTO fd, Airport Origin, Airport Destination)
        {
            this.FlightId = fd.FlightId;
            this.FlightDetailId = fd.FlightDetailId;
            this.RouteId = fd.RouteId;
            this.OriginAirport = Origin.AirportName;
            this.OriginCountry = Origin.Address.Country;
            this.DesAirport = Destination.AirportName;
            this.DesCountry = Destination.Address.Country;
            this.ArrDate = fd.ArrDate.ToString();
            this.DepDate = fd.DepDate.ToString();
        }
        public FlightDetailVM(FlightDetailDTO fd, AirportDTO Origin, AirportDTO Destination)
        {
            this.FlightId = fd.FlightId;
            this.FlightDetailId = fd.FlightDetailId;
            this.RouteId = fd.RouteId;
            this.OriginAirport = Origin.AirportName;
            this.OriginCountry = Origin.Address.Country;
            this.DesAirport = Destination.AirportName;
            this.DesCountry = Destination.Address.Country;
            this.ArrDate = fd.ArrDate.ToString();
            this.DepDate = fd.DepDate.ToString();
        }
    }
    public class CreateFlightVM
    {
        public string PlaneId { get; set; }
        public string Status { get; set; }
        public List<string> routeId { get; set; }
        public List<string> depDate { get; set; }
        public List<string> arrDate { get; set; }
    }
    public class EditFlightVM
    {
        public string FlightId { get; set; }
        public string PlaneId { get; set; }
        public string Status { get; set; }
        public List<string> flightDetailId { get; set; }
        public List<string> routeId { get; set; }
        public List<string> depDate { get; set; }
        public List<string> arrDate { get; set; }
        public EditFlightVM()
        {
            flightDetailId = new List<string>();
            routeId = new List<string>();
            depDate = new List<string>();
            arrDate = new List<string>();
        }
    }

}