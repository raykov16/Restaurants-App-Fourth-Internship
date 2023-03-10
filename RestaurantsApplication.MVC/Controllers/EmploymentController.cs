using Microsoft.AspNetCore.Mvc;
using RestaurantsApplication.DTOs.EmploymentDTOs;
using RestaurantsApplication.MVC.Models.Employment;
using RestaurantsApplication.Services.Contracts;
using static RestaurantsApplication.MVC.Messages.ErrorMessages;
using static RestaurantsApplication.MVC.Messages.SuccessMessages;
using static RestaurantsApplication.MVC.Messages.ProcessErrorMessages;

namespace RestaurantsApplication.MVC.Controllers
{
    public class EmploymentController : Controller
    {
        private readonly IRoleService _roleService;
        private readonly ILocationService _locationService;
        private readonly IDepartmentService _departmentService;
        private readonly IEmploymentService _employmentService;
        private readonly IValidatorService _validatorService;

        public EmploymentController(IRoleService roleService,
            ILocationService locationService,
            IDepartmentService departmentService,
            IValidatorService validatorService,
            IEmploymentService employmentService)
        {
            _roleService = roleService;
            _locationService = locationService;
            _departmentService = departmentService;
            _validatorService = validatorService;
            _employmentService = employmentService;
        }

        [HttpGet]
        public async Task<IActionResult> Add(string employeeCode)
        {
            try
            {
                var model = new EmploymentShortInfoViewModel()
                {
                    StartDate = DateTime.Now,
                    EmployeeCode = employeeCode,
                };

                await MapCollections(model);

                return View(model);
            }
            catch (Exception)
            {
                return RedirectToAction("Error", "Home", new { message = CouldntLoadError });
            }
           
        }

        [HttpPost]
        public async Task<IActionResult> Add(EmploymentShortInfoViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await MapCollections(model);
                    return View(model);
                }

                bool isWrong = false;

                if (model.EndDate != null && model.EndDate < model.StartDate)
                {
                    ModelState.AddModelError(nameof(model.EndDate), EarlierEndDateError);
                    isWrong = true;
                }

                if (!await _validatorService.ValidateDepartmentAsync(model.DepartmentId, model.LocationId))
                {
                    ModelState.AddModelError(nameof(model.DepartmentId), DepartmentDoesntExistInLocationError);
                    isWrong = true;
                }

                model.EmployeeId = await _validatorService.ValidateEmployeeAsync(model.EmployeeCode);

                if (model.EmployeeId == 0)
                {
                    ModelState.AddModelError(nameof(model.EmployeeCode), EmployeeWrongCodeError);
                    await MapCollections(model);
                    return View(model);
                }

                if (model.IsMain && !await _validatorService.CanEmploymentBeMainAsync(model.EmployeeId))
                {
                    ModelState.AddModelError(nameof(model.IsMain), EmployeeAlreadyHasMainEmploymentError);
                    isWrong = true;
                }

                if (!await _validatorService.ValidateEmploymentRoleAsync(model.RoleId, model.DepartmentId, model.EmployeeId))
                {
                    ModelState.AddModelError(nameof(model.RoleId), EmployeeAlreadyHasEmploymentWithRoleError);
                    isWrong = true;
                }

                if (isWrong)
                {
                    await MapCollections(model);
                    return View(model);
                }

                var dto = MapDTO(model);

                await _employmentService.AddAsync(dto);

                TempData["message"] = EmploymentAdded;
                return RedirectToAction("ViewAll", "Employee");
            }
            catch (Exception)
            {
                return RedirectToAction("Error", "Home", new { message = CouldntProcessError });
            }
           
        }

        public async Task<IActionResult> EmployeeEmployments(int employeeId)
        {
            try
            {
                var dtos = await _employmentService.GetEmploymentsForEmployeeAsync(employeeId);

                var models = dtos
                    .Select(d => new EmploymentDetailsViewModel
                    {
                        Id = d.Id,
                        StartDate = d.StartDate,
                        EndDate = d.EndDate,
                        RoleName = d.RoleName,
                        LocationName = d.LocationName,
                        DepartmentName = d.DepartmentName,
                        IsMain = d.IsMain,
                        Rate = d.Rate,
                    })
                    .OrderByDescending(e => e.IsMain)
                    .ToList();

                return View(models);
            }
            catch (Exception)
            {
                return RedirectToAction("Error", "Home", new { message = RetrievingEmploymentsError });
            }
            
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int employmentId)
        {
            try
            {
                var dto = await _employmentService.GetByIdAsync(employmentId);

                var model = new EmploymentShortInfoViewModel
                {
                    DepartmentId = dto.DepartmentId,
                    EmployeeCode = dto.EmployeeCode,
                    Id = dto.Id,
                    EmployeeId = dto.EmployeeId,
                    EndDate = dto.EndDate,
                    IsMain = dto.IsMain,
                    LocationId = dto.LocationId,
                    Rate = dto.Rate,
                    RoleId = dto.RoleId,
                    StartDate = dto.StartDate,
                };

                await MapCollections(model);

                return View(model);
            }
            catch (Exception)
            {
                return RedirectToAction("Error", "Home", new { message = CouldntLoadError });
            }

        }

        public async Task<IActionResult> Edit(EmploymentShortInfoViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await MapCollections(model);
                    return View(model);
                }

                if (model.EndDate != null && model.EndDate < model.StartDate)
                {
                    ModelState.AddModelError(nameof(model.EndDate), EarlierEndDateError);
                }

                if (!await _validatorService.ValidateDepartmentAsync(model.DepartmentId, model.LocationId))
                {
                    ModelState.AddModelError(nameof(model.DepartmentId), DepartmentDoesntExistInLocationError);
                }

                if (model.IsMain
                    && !await _validatorService.CanEmploymentBeMainAsync(model.EmployeeId)
                    && model.Id != await _employmentService.GetMainEmploymentIdAsync(model.EmployeeId))
                {
                    ModelState.AddModelError(nameof(model.IsMain), EmployeeAlreadyHasMainEmploymentError);
                    await MapCollections(model);
                    return View(model);
                }

                var dto = MapDTOWithId(model);

                await _employmentService.EditAsync(dto);

                return RedirectToAction(nameof(EmployeeEmployments), new { employeeId = dto.EmployeeId });
            }
            catch (Exception)
            {
                return RedirectToAction("Error", "Home", new { message = CouldntProcessError });
            }

        }

        public async Task<IActionResult> Delete(int employmentId)
        {
            try
            {
                int employeeId = await _employmentService.DeleteAsync(employmentId);

                if (employeeId == -1)
                {
                    TempData["error"] = CantDeleteMainEmploymentError;
                    return Redirect(Request.GetTypedHeaders().Referer.ToString());
                }
                return RedirectToAction(nameof(EmployeeEmployments), new { employeeId = employeeId });
            }
            catch (Exception)
            {
                return RedirectToAction("Error", "Home", new { message = CouldntProcessError });
            }
           
        }

        private EmploymentWithIdDTO MapDTOWithId(EmploymentShortInfoViewModel model)
        {
            return new EmploymentWithIdDTO
            {
                Id = model.Id,
                DepartmentId = model.DepartmentId,
                EmployeeCode = model.EmployeeCode,
                EmployeeId = model.EmployeeId,
                EndDate = model.EndDate,
                IsMain = model.IsMain,
                LocationId = model.LocationId,
                Rate = model.Rate,
                RoleId = model.RoleId,
                StartDate = model.StartDate
            };
        }

        private EmploymentShortInfoDTO MapDTO(EmploymentShortInfoViewModel model)
        {
            return new EmploymentShortInfoDTO()
            {
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                DepartmentId = model.DepartmentId,
                EmployeeId = model.EmployeeId,
                LocationId = model.LocationId,
                RoleId = model.RoleId,
                IsMain = model.IsMain,
                Rate = model.Rate
            };
        }

        private async Task MapCollections(EmploymentShortInfoViewModel model)
        {
            model.Roles = await _roleService.GetRolesWithIdsAsync();
            model.Locations = await _locationService.GetLocationsWithIdsAsync();
            model.Departments = await _departmentService.GetDepartmentsWithIdsAsync();
        }
    }
}
