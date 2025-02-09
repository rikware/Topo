﻿using FastReport.Export.PdfSimple;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Topo.Models.Logbook;
using Topo.Models.MemberList;
using Topo.Services;

namespace Topo.Controllers
{
    public class LogbookController : Controller
    {
        private readonly StorageService _storageService;
        private readonly IMemberListService _memberListService;
        private ILogbookService _logbookService;
        private ILogger<LogbookController> _logger;

        public LogbookController(StorageService storageService,
            IMemberListService memberListService, ILogbookService logbookService, ILogger<LogbookController> logger)
        {
            _storageService = storageService;
            _memberListService = memberListService;
            _logbookService = logbookService;
            _logger = logger;
        }

        private async Task<LogbookListViewModel> SetUpViewModel(bool includeLeaders = false)
        {
            var model = new LogbookListViewModel();
            model.Units = new List<SelectListItem>();
            if (_storageService.Units != null)
                model.Units = _storageService.Units;
            if (_storageService.SelectedUnitId != null)
            {
                model.SelectedUnitId = _storageService.SelectedUnitId;
                var allMembers = await _memberListService.GetMembersAsync();
                var members = allMembers.Where(m => includeLeaders || m.isAdultLeader == 0).OrderBy(m => m.first_name).ThenBy(m => m.last_name).ToList();
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

        private void SetViewBag()
        {
            ViewBag.IsAuthenticated = _storageService.IsAuthenticated;
            ViewBag.Unit = _storageService.SelectedUnitName;
        }

        public async Task<ActionResult> Index(bool includeLeaders = false)
        {
            var model = await SetUpViewModel(includeLeaders);
            return View(model);
        }

        [HttpPost]
        public async Task<ActionResult> Index(LogbookListViewModel logbookListViewModel, string button)
        {
            if (string.IsNullOrEmpty(logbookListViewModel.SelectedUnitId) || _storageService.SelectedUnitId != logbookListViewModel.SelectedUnitId)
            {
                _storageService.SelectedUnitId = logbookListViewModel.SelectedUnitId;
                return RedirectToAction("Index", "Logbook", new { includeLeaders = logbookListViewModel.IncludeLeaders });
            }

            var model = new LogbookListViewModel();
            if (!string.IsNullOrEmpty(logbookListViewModel.SelectedUnitId))
            {
                _storageService.SelectedUnitId = logbookListViewModel.SelectedUnitId;
                if (!string.IsNullOrEmpty(button))
                {
                    var selectedMembers = logbookListViewModel.getSelectedMembers();
                    if(selectedMembers == null)
                    {
                        _logger.LogInformation("selectedMembers: null");
                        selectedMembers = new List<string>();
                    }
                    // No members selected, default to all
                    if (selectedMembers.Count() == 0)
                    {
                        selectedMembers = logbookListViewModel.Members.Select(m => m.id).ToList();
                    }
                    if (selectedMembers != null)
                    {
                        _logger.LogInformation($"selectedMembers.Count: {selectedMembers.Count()}");
                        var memberKVP = new List<KeyValuePair<string, string>>();
                        foreach (var member in selectedMembers)
                        {
                            var memberName = logbookListViewModel.Members.Where(m => m.id == member).Select(m => m.first_name + " " + m.last_name).FirstOrDefault();
                            memberKVP.Add(new KeyValuePair<string, string>(member, memberName ?? ""));
                        }
                        _logger.LogInformation($"memberKVP.Count: {memberKVP.Count()}");
                        var report = await _logbookService.GenerateLogbookReport(memberKVP);
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

                            SetViewBag();
                            // return stream in browser
                            var unitName = _storageService.SelectedUnitName ?? "";
                            return File(strm, "application/pdf", $"Logbook_Report_{unitName.Replace(' ', '_')}.pdf");
                        }
                    }
                }
            }
            return RedirectToAction("Index", "Logbook", new { includeLeaders = logbookListViewModel.IncludeLeaders });
        }

    }
}
