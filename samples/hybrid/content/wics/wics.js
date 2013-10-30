$(document).ready(function()
{
  wics_init();

  function wics_init()
  {
    wics_prepare_design();

    $('.wics-hint').click(function(event) {
      event.stopPropagation();
      wics_process_click($(this));
      $('.wics-note').click(function() {
        wics_activate(wics_hints[$(this).attr('wics-id')]);
      });
      $('.wics-note-close').click(function(event) {
        event.stopPropagation();
        var hint = wics_hints[$(this).closest('.wics-note').attr('wics-id')];
        hint.opened = false;
        hint.ref.removeClass('wics-active');
        hint.note.remove();
        wics_update_footnotes();
      });
    });
  }
  
  function wics_prepare_design()
  {
    var body = $('BODY');
    body.css('margin', 0);
    body.html('<div id="wics-upperframe">' + body.html() + '</div><div id="wics-lowerframe"></div>');
    upperframe = $('#wics-upperframe');
    lowerframe = $('#wics-lowerframe');
    
    $(window).resize(function() { wics_resize(); });
    
    wics_resize();
  }
  
  function wics_resize()
  {
    var height = $(window).height();
    var height1 = Math.floor(height * 0.50 - 30), height2 = height1;
    lowerframe.height(height2);
    upperframe.height(height1);
  }

  function wics_make_note(hint)
  {
    var text = "";

    // Translate
    if (hint.translate == "no") text += "Do not translate<br/>"

    // Localization Note
    if (hint.locNote) text += hint.locNote + "<br/>";
    else if (hint.locNoteRef) text += '<a href="' + hint.locNoteRef + '">' + hint.locNoteRef + "</a><br/>";

    // Terminology
    if (hint.termInfo) text += hint.termInfo + "<br/>";
    else if (hint.termInfoRef) text += '<a href="' + hint.termInfoRef + '">' + hint.termInfoRef + "</a><br/>";

    // Directionality
    switch (hint.dir)
    {
      case "ltr": text += "Reading left-to-right<br/>"; break;
      case "rtl": text += "Reading right-to-left<br/>"; break;
      case "lro": text += "Reading left-to-right override<br/>"; break;
      case "rlo": text += "Reading right-to-left override<br/>";
    }

    // Language Information
    if (hint.lang) text += "Language: " + hint.lang + "<br/>";

    // Elements Within Text
    switch (hint.withinText)
    {
      case "yes":    text += "Internal tag<br/>"; break;
      case "nested": text += "Independent insert<br/>"; break;
      case "no":     text += "External tag – end of text unit<br/>";
    }

    // Domain
    if (hint.domains) text += "Domain: " + hint.domains + "<br/>";

    // Text Analysis
    if (hint.taConfidence) text += "Confidence: " + hint.taConfidence + "<br/>";
    if (hint.taIdentRef)
      text += 'Ident: <a href="' + hint.taIdentRef + '">' + hint.taIdentRef + "</a><br/>";
    else
    {
      if (hint.taSource) text += "Source: " + hint.taSource + "<br/>";
      if (hint.taIdent) text += "Ident: " + hint.taIdent + "<br/>";
    }
    if (hint.taClassRef)
      text += 'Class: <a href="' + hint.taClassRef + '">' + hint.taClassRef + "</a><br/>";

    // Locale Filter
    if (hint.localeFilterList || hint.localeFilterType)
    {
      text += "Locale: ";
      if (hint.localeFilterType == "exclude")
        text += hint.localeFilterList == ""? "any" : hint.localeFilterList == "*"? "none" : ("all excluding " + hint.localeFilterList);
      else
        text += hint.localeFilterList == "*"? "any" : hint.localeFilterList;
      text += "<br/>";
    }

    // Provenance
    function wics_make_provenance_note(r)
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
    if (hint.provenanceRecordsRef)
      for (var i = 0; i < hint.provenanceRecordsRef.length; ++i)
      {
        if (i > 0) text += "<br/>";
        text += wics_make_provenance_note(hint.provenanceRecordsRef[i]);
      }
    else text += wics_make_provenance_note(hint);

    // Target Pointer
    if (hint.targetPointer) text += "Source<br/>";
    if (hint.target) text += "Target<br/>";

    // Id Value
    if (hint.idValue) text += "ID: " + hint.idValue + "<br/>";

    // Localization Quality Issue
    function wics_make_issue_note(issue)
    {
      var text = "";
      if (issue.locQualityIssueType) text += "Type: " + issue.locQualityIssueType + "<br/>";
      if (issue.locQualityIssueComment) text += "Comment: " + issue.locQualityIssueComment + "<br/>";
      if (issue.locQualityIssueSeverity) text += "Severity: " + issue.locQualityIssueSeverity + "<br/>";
      if (issue.locQualityIssueProfileRef) text += 'Profile: <a href="' + issue.locQualityIssueProfileRef + '">' + issue.locQualityIssueProfileRef + "</a><br/>";
      if (issue.locQualityIssueEnabled) text += "Enabled: " + issue.locQualityIssueEnabled + "<br/>";
      return text;
    }
    if (hint.locQualityIssueType || hint.locQualityIssueComment)
      text += wics_make_issue_note(hint);
    else if (hint.locQualityIssuesRef)
      for (var i = 0; i < hint.locQualityIssuesRef.length; ++i)
      {
        if (i > 0) text += "<br/>";
        text += wics_make_issue_note(hint.locQualityIssuesRef[i]);
      }

    // Localization Quality Rating
    function wics_make_rating_note(score, threshold)
    {
      var text = "Score: " + score + "<br/>";
      if (threshold) text += "Score threshold: " + threshold + "<br/>";
      return text;
    }
    if (hint.locQualityRatingScore)
      text += wics_make_rating_note(hint.locQualityRatingScore, hint.locQualityRatingScoreThreshold);
    else if (hint.locQualityRatingVote)
      text += wics_make_rating_note(hint.locQualityRatingVote, hint.locQualityRatingVoteThreshold);

    // MT Confidence
    if (hint.mtConfidence) text += "MT confidence: " + hint.mtConfidence + "<br/>";

    // Allowed Characters
    if (hint.allowedCharacters) text += "Allowed characters: " + hint.allowedCharacters + "<br/>";

    // Storage Size
    if (hint.storageSize)
    {
      text += "Storage size (bytes): " + hint.storageSize + "<br/>";
      if (hint.storageEncoding) text += "Encoding: " + hint.storageEncoding + "<br/>";
      if (hint.lineBreakType) text += "Line break type: " + hint.lineBreakType + "<br/>";
    }

    return text;
  }

  function wics_process_click(obj)
  {
    var o = obj.closest('.wics-hint');
    var i = o.attr('wics-id');
    if (!i) return;
    var hint = wics_hints[i];
    if (!hint.opened)
    {
      hint.opened = true;
      hint.ref = o;
      var prev;
      for (var j = i - 1; j >= 0; --j)
        if (wics_hints[j].opened) 
          { prev = wics_hints[j].note; break; }
      var hint_html = '<div wics-id="' + i + '" class="wics-note">' +
                       '<div class="wics-note-top"><div class="wics-note-name"> ' + o.text().replace(/[\r\n ]+/g, ' ').replace(/(?:(?:^\s+)|(?:\s+$)|(?:<[^>]+>))/g, '') + '</div><div class="wics-note-close">Close</div></div>' +
                       '<div class="wics-note-text">' + wics_make_note(hint) + '</div>' +
                      '</div>';
      if (prev) prev.after(hint_html); else lowerframe.prepend(hint_html);
      hint.note = $('div[wics-id=' + i + ']', lowerframe);
      wics_update_footnotes();
    }
    wics_activate(hint);
  }

  function wics_activate(hint)
  {
    $('.wics-active', upperframe).removeClass('wics-active');
    hint.ref.addClass('wics-active');
    $('.wics-note-active', lowerframe).removeClass('wics-note-active');
    hint.note.addClass('wics-note-active');
  }
  
  function wics_update_footnotes()
  {
    $('.wics-sup').remove();
     
    var count = 0;
    for (var i = 0; i < wics_hints.length; ++i)
      if (wics_hints[i].opened)
      {
        var sup = '<sup class="wics-sup">' + (++count) + '</span>';
        wics_hints[i].ref.after(sup);
        $('.wics-note-name', wics_hints[i].note).prepend(sup);
      }
  }
});
