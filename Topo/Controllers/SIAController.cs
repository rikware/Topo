﻿using FastReport.Export.PdfSimple;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Topo.Models.MemberList;
using Topo.Models.SIA;
using Topo.Services;

namespace Topo.Controllers
{
    public class SIAController : Controller
    {
        private readonly StorageService _storageService;
        private readonly IMemberListService _memberListService;
        private readonly ISIAService _SIAService;
        private readonly ILogger<SIAController> _logger;

        public SIAController(StorageService storageService, IMemberListService memberListService, ISIAService sIAService, ILogger<SIAController> logger)
        {
            _storageService = storageService;
            _memberListService = memberListService;
            _SIAService = sIAService;
            _logger = logger;
        }

        private void SetViewBag()
        {
            ViewBag.IsAuthenticated = _storageService.IsAuthenticated;
            ViewBag.Unit = _storageService.SelectedUnitName;
        }

        private async Task<SIAIndexViewModel> SetUpViewModel()
        {
            var model = new SIAIndexViewModel();
            model.Units = new List<SelectListItem>();
            if (_storageService.Units != null)
                model.Units = _storageService.Units;
            if (_storageService.SelectedUnitId != null)
            {
                model.SelectedUnitId = _storageService.SelectedUnitId;
                var allMembers = await _memberListService.GetMembersAsync();
                var members = allMembers.Where(m => m.isAdultLeader == 0).OrderBy(m => m.first_name).ThenBy(m => m.last_name).ToList();
                foreach (var member in members)
                {
                    var editorViewModel = new MemberListEditorViewModel
                    {
                        id = member.id,
                        first_name = member.first_name,
                        last_name = member.last_name,
                        member_number = member.member_number,
                        patrol_name = string.IsNullOrEmpty(member.patrol_name) ? "-" : member.patrol_name,
                        patrol_duty = string.IsNullOrEmpty(member.patrol_duty) ? "-" : member.patrol_duty,
                        unit_council = member.unit_council,
                        selected = false
                    };
                    model.Members.Add(editorViewModel);
                }
            }
            if (_storageService.Units != null)
            {
                _storageService.SelectedUnitName = _storageService.Units.Where(u => u.Value == _storageService.SelectedUnitId)?.FirstOrDefault()?.Text;
                model.SelectedUnitName = _storageService.SelectedUnitName ?? "";
            }
            SetViewBag();
            return model;
        }

        // GET: SIAController
        public async Task<ActionResult> Index()
        {
            var model = await SetUpViewModel();
            return View(model);
        }

        [HttpPost]
        public async Task<ActionResult> Index(SIAIndexViewModel siaIndexViewModel, string button)
        {
            if (string.IsNullOrEmpty(siaIndexViewModel.SelectedUnitId) || _storageService.SelectedUnitId != siaIndexViewModel.SelectedUnitId)
            {
                _storageService.SelectedUnitId = siaIndexViewModel.SelectedUnitId;
                return RedirectToAction("Index", "SIA");
            }

            var model = new SIAIndexViewModel();
            if (!string.IsNullOrEmpty(siaIndexViewModel.SelectedUnitId))
            {
                _storageService.SelectedUnitId = siaIndexViewModel.SelectedUnitId;
                if (!string.IsNullOrEmpty(button))
                {
                    var selectedMembers = siaIndexViewModel.getSelectedMembers();
                    if (selectedMembers == null)
                    {
                        _logger.LogInformation("selectedMembers: null");
                        selectedMembers = new List<string>();
                    }
                    // No members selected, default to all
                    if (selectedMembers.Count() == 0)
                    {
                        selectedMembers = siaIndexViewModel.Members.Select(m => m.id).ToList();
                    }
                    if (selectedMembers != null)
                    {
                        _logger.LogInformation($"selectedMembers.Count: {selectedMembers.Count()}");
                        var memberKVP = new List<KeyValuePair<string, string>>();
                        foreach (var member in selectedMembers)
                        {
                            var memberName = siaIndexViewModel.Members.Where(m => m.id == member).Select(m => m.first_name + " " + m.last_name).FirstOrDefault();
                            memberKVP.Add(new KeyValuePair<string, string>(member, memberName ?? ""));
                        }
                        _logger.LogInformation($"memberKVP.Count: {memberKVP.Count()}");
                        var report = await _SIAService.GenerateSIAReport(memberKVP);
                        if (report.Prepare())
                        {
                            // Set PDF export props
                            PDFSimpleExport pdfExport = new PDFSimpleExport();
                            pdfExport.ShowProgress = false;

                            MemoryStream strm = new MemoryStream();
                            report.Report.Export(pdfExport, strm);
                            report.Dispose();
                            pdfExport.Dispose();
                            strm.Position = 0;

                            // return stream in browser
                            var unitName = _storageService.SelectedUnitName ?? "";
                            return File(strm, "application/pdf", $"SIA_Projects_{unitName.Replace(' ', '_')}.pdf");
                        }
                    }
                }
            }
            model = await SetUpViewModel();
            return View(model);
        }
    }
}

