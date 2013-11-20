$(document).ready(function()
{
  var wics = {

  fragments: 'p,div',

  init: function()
  {
    var org_body = $('BODY').replaceWith('<body><div id="wics-navbar"><i class="wics-first-fragment">&lt;&lt; First Fragment <span>[Ctrl+Shift+&#8593;]</span></i> <i class="wics-prev-fragment">&lt; Previous Fragment <span>[Ctrl+&#8593;]</span></i> <i class="wics-next-fragment">Next Fragment &gt; <span>[Ctrl+&#8595;]</span></i> <i class="wics-last-fragment">Last Fragment &gt;&gt; <span>[Ctrl+Shift+&#8595;]</span></i> <i class="wics-goto-fragment">Go To The Fragment <span>[Alt+Enter]</span></i> <i class="wics-open-all-tips">Open All Tips <span>[Shift+Enter]</span></i> <i class="wics-close-all-tips">Close All Tips <span>[Shift+Backspace]</span></i><br/><i class="wics-first-tip">&lt;&lt; First Tip <span>[Ctrl+Shift+&#8592;]</span></i> <i class="wics-prev-tip">&lt; Previous Tip <span>[Ctrl+&#8592;]</span></i> <i class="wics-next-tip">Next Tip &gt; <span>[Ctrl+&#8594;]</span></i> <i class="wics-last-tip">Last Tip &gt;&gt; <span>[Ctrl+Shift+&#8594;]</span></i> <i class="wics-goto-tip">Go To The Tip <span>[Alt+Shift+Enter]</span></i> <i class="wics-open-tip">Open Tip <span>[Enter]</span></i> <i class="wics-close-tip">Close Tip <span>[Backspace]</span></i></div>' +
                                    '<div id="wics-upperframe"></div><div id="wics-lowerframe"></div></body>');
    this.upperframe = $('#wics-upperframe');
    this.upperframe.append(org_body);
    this.lowerframe = $('#wics-lowerframe');
    this.navbar = $('#wics-navbar');
    
    $(window).resize(function() { wics.resize(); });
    this.resize();

    for (var i = 0; i < wics_tips.length; ++i)
    {
      wics_tips[i].id = i;
      wics_tips[i].node = $('span[wics-id="' + i + '"]', this.upperframe);
    }

    this.parts = [ { tips: wics_tips, selected: 0 } ];
    if (this.fragments != null)
    {
      var parts = $(this.fragments, this.upperframe), part, tips, tip;
      for (i = 0; i < parts.length; ++i)
      {
        part = { node: parts.eq(i), tips: [], selected: 0 };
        tips = $('.wics-tip', part.node);
        if (tips.length > 0)
        {
          for (var j = 0; j < tips.length; ++j)
          {
            tip = wics_tips[tips.eq(j).attr('wics-id')];
            tip.part = this.parts.length;
            part.tips.push(tip);
          }
          part.tips.sort(by_wics_id);
          this.parts.push(part);
        }
        part.node.addClass('wics-fragment');
      }
      if (this.parts.length > 1)
      {
        part = { tips: [], selected: 0 };
        for (i = 0; i < wics_tips.length; ++i)
        {
          tip = wics_tips[i];
          if (!tip.part)
          {
            tip.part = this.parts.length;
            part.tips.push(tip);
          }
        }
        if (part.tips.length > 0) part.tips.sort(by_wics_id), this.parts.push(part);
      }
    }
    this.select_part(this.parts.length > 1? 1 : 0);

    $('.wics-no-tip').click(function(event) { event.stopPropagation(); });
    $('.wics-tip').click(function(event) {
      event.stopPropagation();
      wics.process_click($(this));
    });

    $('.wics-next-tip').click(function() { wics.next_tip(); });
    $('.wics-prev-tip').click(function() { wics.prev_tip(); });
    $('.wics-next-fragment').click(function() { wics.next_part(); });
    $('.wics-prev-fragment').click(function() { wics.prev_part(); });
    $('.wics-first-tip').click(function() { wics.select_tip(0); });
    $('.wics-last-tip').click(function() { wics.select_tip(wics.parts[wics.selected].tips.length - 1); });
    $('.wics-first-fragment').click(function() { wics.select_part(1); });
    $('.wics-last-fragment').click(function() { wics.select_part(wics.parts.length - 1);});
    $('.wics-goto-tip').click(function() { wics.open_goto_window(true);});
    $('.wics-goto-fragment').click(function() { wics.open_goto_window(false); });
    $('.wics-open-tip').click(function() { wics.open_tip(); });
    $('.wics-close-tip').click(function() { wics.close_tip(); });
    $('.wics-open-all-tips').click(function() { wics.open_all_tips(); });
    $('.wics-close-all-tips').click(function() { wics.close_all_tips(); });

    $(document).unbind('keypress');
    $(document).keydown(function(event) {
      if (event.ctrlKey == true)
        switch (event.which)
        {
          case 38:
            if (event.shiftKey == true)
              wics.select_part(1);
            else
              wics.prev_part();
            break;

          case 40:
            if (event.shiftKey == true)
              wics.select_part(navigation.fragmentsCount);
            else
              wics.next_part();
            break;

          case 37:
            if (event.shiftKey == true)
              wics.select_tip(0);
            else
              wics.prev_tip();
            break;
      
          case 39:
            if (event.shiftKey == true)
              wics.select_tip(wics.parts[wics.selected].tips.length - 1);
            else
              wics.next_tip();
        }
      else if (event.which == 13)
      {
      	if (event.altKey == true)
          wics.open_goto_window(event.shiftKey);
      	else if (event.shiftKey == true)
          wics.open_all_tips();
        else
          wics.open_tip();
      }
      else if (event.which == 8)
      {
      	if (event.shiftKey == true)
          wics.close_all_tips();
      	else
          wics.close_tip();
      }
    });
  },
    
  resize : function()
  {
    var height = $(window).height() - this.navbar.height();
    var height1 = Math.floor(height * 0.50 - 30), height2 = height1;
    this.lowerframe.height(height2);
    this.upperframe.height(height1);
  },

  process_click : function(obj)
  {
    var i = obj.closest('.wics-tip').attr('wics-id');
    if (i)
    {
      var tip = wics_tips[i];
      if (this.selected > 0 && tip.part != this.selected)
        this.select_part(tip.part);
      this.open_tip(tip);
      this.select_tip(tip.index);
    }
  },

  select_part : function(i)
  {
    if (this.selected > 0 && i < 1) i = 1; else if (i >= this.parts.length) i = this.parts.length - 1;
    var part = this.parts[this.selected = i];
    for (i = 0; i < part.tips.length; ++i) part.tips[i].index = i;
    $('.wics-fragment-selected', this.upperframe).removeClass('wics-fragment-selected');
    if (part.node) part.node.addClass('wics-fragment-selected');
    this.select_tip(part.selected);
    this.update_notes();
  },

  next_part : function()
  {
    var i = this.selected;
    if (i > 0) this.select_part(++i < this.parts.length? i : 1);
  },

  prev_part : function()
  {
    var i = this.selected;
    if (i > 0) this.select_part(--i > 0? i : this.parts.length - 1);
  },

  select_tip : function(i)
  {
    var part = this.parts[this.selected];
    $('.wics-tip-selected', this.upperframe).removeClass('wics-tip-selected');
    if (i < 0) i = 0; else if (i >= part.tips.length) i = part.tips.length - 1;
    var tip = part.tips[part.selected = i];
    tip.node.addClass('wics-tip-selected');
    $('.wics-note-selected', this.lowerframe).removeClass('wics-note-selected');
    if (tip.node) tip.node[0].scrollIntoView(false);
    if (tip.opened)
    {
      tip.note[0].scrollIntoView(false);
      tip.note.addClass('wics-note-selected');
    }
  },

  next_tip : function()
  {
    var part = this.parts[this.selected], i = part.tips[part.selected].index;
    this.select_tip(++i < part.tips.length? i : 0);
  },

  prev_tip : function()
  {
    var part = this.parts[this.selected], i = part.tips[part.selected].index;
    this.select_tip(--i >= 0? i : part.tips.length - 1);
  },

  open_tip : function(tip)
  {
    var part = this.parts[this.selected];
    if (!tip) tip = part.tips[part.selected];
    if (tip.opened) return;

    $('.wics-note-selected', this.lowerframe).removeClass('wics-note-selected');
    tip.opened = true;
    var prev;
    for (var j = tip.index - 1; j >= 0; --j)
      if (part.tips[j].opened) 
        { prev = part.tips[j].note; break; }
    $('.wics-sup', tip.node).remove();
    var note_html = make_note_html(tip);
    if (prev) prev.after(note_html); else this.lowerframe.prepend(note_html);
    tip.note = $('div[wics-id="' + tip.id + '"]', this.lowerframe);
    set_note_event_handlers();
    this.update_footnotes();
  },

  close_tip : function(i)
  {
    var part = this.parts[this.selected];
    if (i == undefined) i = part.selected;
    var tip = part.tips[i];
    if (!tip.opened) return;
    tip.opened = false;
    tip.note.remove();
    this.update_footnotes();
  },
  
  open_all_tips : function()
  {
    var part = this.parts[this.selected];
    for (var i = 0; i < part.tips.length; ++i) part.tips[i].opened = true;
    this.update_notes();
  },

  close_all_tips : function()
  {
    var part = this.parts[this.selected];
    for (var i = 0; i < part.tips.length; ++i) part.tips[i].opened = false;
    this.update_notes();
  },

  in_goto : false,  

  open_goto_window : function(tip)
  {
    if (this.in_goto) return;
    this.in_goto = true;
    var text = tip? 'Tip number: ' : 'Fragment number: ';
    $('#wics-goto-window').remove();
    $('BODY').append('<div id="wics-goto-window">' + text + '&nbsp;&nbsp;<input type="text" id="wics-goto-num"><br/><br/><input type="button" value="OK" id="wics-goto-ok" class="wics-goto-button">&nbsp;&nbsp;&nbsp;<input type="button" value="Cancel" id="wics-goto-cancel" class="wics-goto-button"></div>');
    
    $('#wics-goto-cancel').click(function() { wics.close_goto_window(); });
    $('#wics-goto-ok').click(function()
    {
      var num = Number($('#wics-goto-num').val());
      if (isNaN(num)) { alert('Invalid number: ' + $('#wics-goto-num').val()); return; }
      if (tip) wics.select_tip(num - 1); else wics.select_part(num);
      wics.close_goto_window();
    });
    
    $('#wics-goto-window').css('left', (($('BODY').width() / 2) - 80) + 'px');
    $('#wics-goto-num').focus();
  },

  close_goto_window : function()
  {
    if (this.in_goto)
    {
      $('#wics-goto-window').remove();
      this.in_goto = false;
    }
  },

  update_notes : function()
  {
    var part = this.parts[this.selected], note_html = "", i;
    for (i = 0; i < part.tips.length; ++i)
      if (part.tips[i].opened) note_html += make_note_html(part.tips[i]);
    this.lowerframe.empty();
    this.lowerframe.append(note_html);
    for (i = 0; i < part.tips.length; ++i)
    {
      var tip = part.tips[i];
      if (tip.opened)
      {
        tip.note = $('div[wics-id="' + tip.id + '"]', this.lowerframe);
        if (i == part.selected) tip.note.addClass('wics-note-selected');
      }
    }
    set_note_event_handlers();
    this.update_footnotes();
  },

  update_footnotes : function()
  {
    $('.wics-sup').remove();

    var tips = this.parts[this.selected].tips;
    var count = 0;
    for (var i = 0; i < tips.length; ++i)
      if (tips[i].opened)
      {
        var sup = '<sup class="wics-sup" dir="ltr">' + (++count) + '</sup>';
        tips[i].node.after(sup);
        $('.wics-note-name', tips[i].note).prepend(sup);
      }
  }
  };

  function by_wics_id(a, b)
  {
    return a.node.attr('wics-id') - b.node.attr('wics-id');
  }

  function set_note_event_handlers()
  {
    $('.wics-note').click(function() {
      wics.select_tip(wics_tips[$(this).attr('wics-id')].index);
    });
    $('.wics-note-close').click(function(event) {
      event.stopPropagation();
      wics.close_tip(wics_tips[$(this).closest('.wics-note').attr('wics-id')].index);
    });
  }

  function make_note_html(tip)
  {
    var text = tip.node.text();
    if (!text)
    {
      var img = $('img[alt]', tip.node);
      if (img.length > 0) text = img.eq(0).attr("alt"); else text = '';
    }
    return '<div wics-id="' + tip.id + '" class="wics-note">' +
            '<div class="wics-note-top"><div class="wics-note-name"> ' + text.replace(/[\r\n ]+/g, ' ').replace(/(?:(?:^\s+)|(?:\s+$)|(?:<[^>]+>))/g, '') + '</div><div class="wics-note-close">Close</div></div>' +
            '<div class="wics-note-text">' + make_note_text(tip) + '</div>' +
           '</div>';
  }

  function make_note_text(tip)
  {
    var text = "";

    // Translate
    if (tip.translate == "no") text += "Do not translate<br/>"

    // Localization Note
    if (tip.locNote) text += tip.locNote + "<br/>";
    else if (tip.locNoteRef) text += '<a href="' + tip.locNoteRef + '">' + tip.locNoteRef + "</a><br/>";

    // Terminology
    if (tip.termInfo) text += tip.termInfo + "<br/>";
    else if (tip.termInfoRef) text += '<a href="' + tip.termInfoRef + '">' + tip.termInfoRef + "</a><br/>";

    // Directionality
    switch (tip.dir)
    {
      case "ltr": text += "Reading left-to-right<br/>"; break;
      case "rtl": text += "Reading right-to-left<br/>"; break;
      case "lro": text += "Reading left-to-right override<br/>"; break;
      case "rlo": text += "Reading right-to-left override<br/>";
    }

    // Language Information
    if (tip.lang) text += "Language: " + tip.lang + "<br/>";

    // Elements Within Text
    switch (tip.withinText)
    {
      case "yes":    text += "Internal tag<br/>"; break;
      case "nested": text += "Independent insert<br/>"; break;
      case "no":     text += "External tag – end of text unit<br/>";
    }

    // Domain
    if (tip.domains) text += "Domain: " + tip.domains + "<br/>";

    // Text Analysis
    if (tip.taConfidence) text += "Confidence: " + tip.taConfidence + "<br/>";
    if (tip.taIdentRef)
      text += 'Ident: <a href="' + tip.taIdentRef + '">' + tip.taIdentRef + "</a><br/>";
    else
    {
      if (tip.taSource) text += "Source: " + tip.taSource + "<br/>";
      if (tip.taIdent) text += "Ident: " + tip.taIdent + "<br/>";
    }
    if (tip.taClassRef)
      text += 'Class: <a href="' + tip.taClassRef + '">' + tip.taClassRef + "</a><br/>";

    // Locale Filter
    if (tip.localeFilterList || tip.localeFilterType)
    {
      text += "Locale: ";
      if (tip.localeFilterType == "exclude")
        text += tip.localeFilterList == ""? "any" : tip.localeFilterList == "*"? "none" : ("all excluding " + tip.localeFilterList);
      else
        text += tip.localeFilterList == "*"? "any" : tip.localeFilterList;
      text += "<br/>";
    }

    // Provenance
    function make_provenance_note(r)
    {
      var text = "";
      if (r.person) text += "Person: " + r.person + "<br/>";
      else if (r.personRef) text += 'Person: <a href="' + r.personRef + '">' + r.personRef + "</a><br/>";
      if (r.org) text += "Org: " + r.org + "<br/>";
      else if (r.orgRef) text += 'Org: <a href="' + r.orgRef + '">' + r.orgRef + "</a><br/>";
      if (r.tool) text += "Tool: " + r.tool + "<br/>";
      else if (r.toolRef) text += 'Tool: <a href="' + r.toolRef + '">' + r.toolRef + "</a><br/>";
      if (r.revPerson) text += "Revision person: " + r.revPerson + "<br/>";
      else if (r.revPersonRef) text += 'Revision person: <a href="' + r.revPersonRef + '">' + r.revPersonRef + "</a><br/>";
      if (r.revOrg) text += "Revision org: " + r.revOrg + "<br/>";
      else if (r.revOrgRef) text += 'Revision org: <a href="' + r.revOrgRef + '">' + r.revOrgRef + "</a><br/>";
      if (r.revTool) text += "Revision tool: " + r.revTool + "<br/>";
      else if (r.revToolRef) text += 'Revision tool: <a href="' + r.revToolRef + '">' + r.revToolRef + "</a><br/>";
      if (r.provRef)
      {
        text += 'External info:';
        var refs = r.provRef.split(' ');
        for (var i = 0; i < refs.length; ++i)
          text += ' <a href="' + refs[i] + '">' + refs[i] + "</a>";
        text += "<br/>";
      }
      return text;
    }

    if (tip.provenanceRecordsRef)
      for (var i = 0; i < tip.provenanceRecordsRef.length; ++i)
      {
        if (i > 0) text += "<br/>";
        text += make_provenance_note(tip.provenanceRecordsRef[i]);
      }
    else text += make_provenance_note(tip);

    // Target Pointer
    if (tip.targetPointer) text += "Source<br/>";
    if (tip.target) text += "Target<br/>";

    // Id Value
    if (tip.idValue) text += "ID: " + tip.idValue + "<br/>";

    // Localization Quality Issue
    function make_issue_note(issue)
    {
      var text = "";
      if (issue.locQualityIssueType) text += "Type: " + issue.locQualityIssueType + "<br/>";
      if (issue.locQualityIssueComment) text += "Comment: " + issue.locQualityIssueComment + "<br/>";
      if (issue.locQualityIssueSeverity) text += "Severity: " + issue.locQualityIssueSeverity + "<br/>";
      if (issue.locQualityIssueProfileRef) text += 'Profile: <a href="' + issue.locQualityIssueProfileRef + '">' + issue.locQualityIssueProfileRef + "</a><br/>";
      if (issue.locQualityIssueEnabled) text += "Enabled: " + issue.locQualityIssueEnabled + "<br/>";
      return text;
    }

    if (tip.locQualityIssueType || tip.locQualityIssueComment)
      text += make_issue_note(tip);
    else if (tip.locQualityIssuesRef)
      for (var i = 0; i < tip.locQualityIssuesRef.length; ++i)
      {
        if (i > 0) text += "<br/>";
        text += make_issue_note(tip.locQualityIssuesRef[i]);
      }

    // Localization Quality Rating
    function make_rating_note(score, threshold)
    {
      var text = "Score: " + score + "<br/>";
      if (threshold) text += "Score threshold: " + threshold + "<br/>";
      return text;
    }
    if (tip.locQualityRatingScore)
      text += make_rating_note(tip.locQualityRatingScore, tip.locQualityRatingScoreThreshold);
    else if (tip.locQualityRatingVote)
      text += make_rating_note(tip.locQualityRatingVote, tip.locQualityRatingVoteThreshold);

    // MT Confidence
    if (tip.mtConfidence) text += "MT confidence: " + tip.mtConfidence + "<br/>";

    // Allowed Characters
    if (tip.allowedCharacters) text += "Allowed characters: " + tip.allowedCharacters + "<br/>";

    // Storage Size
    if (tip.storageSize)
    {
      text += "Storage size (bytes): " + tip.storageSize + "<br/>";
      if (tip.storageEncoding) text += "Encoding: " + tip.storageEncoding + "<br/>";
      if (tip.lineBreakType) text += "Line break type: " + tip.lineBreakType + "<br/>";
    }

    return text;
  }

  wics.init();
});
