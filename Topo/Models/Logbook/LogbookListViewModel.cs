﻿using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using Topo.Models.MemberList;

namespace Topo.Models.Logbook
{
    public class LogbookListViewModel
    {
        [Display(Name = "Unit")]
        public string SelectedUnitId { get; set; } = string.Empty;
        public string SelectedUnitName { get; set; } = string.Empty;
        public IEnumerable<SelectListItem>? Units { get; set; }
        public List<MemberListEditorViewModel> Members { get; set; }
        public LogbookListViewModel()
        {
            Members = new List<MemberListEditorViewModel>();
        }
        public IEnumerable<string> getSelectedMembers()
        {
            // Return an Enumerable containing the Id's of the selected people:
            return (from m in this.Members where m.selected select m.id).ToList();
        }
        public bool IncludeLeaders { get; set; }
    }
}
