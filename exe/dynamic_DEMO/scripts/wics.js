var wics = new Object();
wics.allowedElements = 'SPAN[translate],SPAN[its-loc-note],SPAN[its-term],SPAN[its-within-text]'; //jQuery selector for processed HTML elements
wics.rules = new Array();
wics.elemCounter = 0;
wics.ruleTypes = [['locNoteRule', 'LOCNOTE'], ['translateRule', 'TRANSLATE']];
wics.appendedFunction = function(){};
wics.showLowerFrame = false;

_f_tags = '#upperFrame P, #upperFrame DIV';

$(document).ready(function(){
	// --- NON-INIT FUNCTIONS --- //
	
	//navigation
	
	var navigation = {
		fragmentsCount : 0,
		fragmentsPointer: 1,
		tipsCount: 0,
		tipsPointer: 1,
		gotoOpened: false,
		countFragments: function(){
			navigation.fragmentsCount = $(_f_tags).length;
		},
		countTips: function(){
			navigation.tipsCount = $(".selected-fragment").find(wics.allowedElements).length;
		},
		nextFragment: function(){
			if (navigation.fragmentsPointer !== navigation.fragmentsCount){
				navigation.selectFragment(navigation.fragmentsPointer+1);
			}
			wics.showLowerFrame = false;
			prepareFramesSize();
		},
		previousFragment: function(){
			if (navigation.fragmentsPointer !== 1){
				navigation.selectFragment(navigation.fragmentsPointer-1);
			}
			wics.showLowerFrame = false;
			prepareFramesSize();
		},
		nextTip: function(){
			if (navigation.tipsPointer !== navigation.tipsCount){
				navigation.selectTip(navigation.tipsPointer+1);
			}
		},
		previousTip: function(){
			if (navigation.tipsPointer !== 1){
				navigation.selectTip(navigation.tipsPointer-1);
			}
		},
		selectFragment: function(num){
			navigation.closeAllTips();
			navigation.fragmentsPointer = num;
			$(_f_tags).removeClass('selected-fragment');
			$(_f_tags).eq(navigation.fragmentsPointer-1).addClass('selected-fragment');
			var topPos = $(_f_tags).eq(navigation.fragmentsPointer-1).position();
			$('#upperFrame').scrollTop(topPos.top-30);
			navigation.countTips();
			if (navigation.tipsCount > 0) navigation.selectTip(1);
		},
		selectTip: function(num){
			navigation.tipsPointer = num;
			$(wics.allowedElements).removeClass('selected-tip');
			$(".selected-fragment").find(wics.allowedElements).eq(navigation.tipsPointer-1).addClass('selected-tip');
			//var topPos = $(".selected-fragment SPAN").eq(navigation.fragmentsPointer-1).position();
			//$('#upperFrame').scrollTop(topPos.top-30);
		},
		
		openCloseTip: function(){
			var elem_id = $('.selected-tip').attr('wicspointer').replace('elem-', '');
			if (wics.rules[elem_id].opened == false){
				$('.selected-tip').click();
			}
			else
			{
				wics.rules[elem_id].opened = false;
				$('#wics-hint-elem-'+elem_id).remove();
				prepareNumeration();
				$('[wicspointer="elem-'+elem_id+'"]').removeClass('activeHint');
			}
		},
		
		openAllTips: function(){
			$('.selected-fragment').find(wics.allowedElements).click();
		},
		
		closeAllTips: function(){
			$('.selected-fragment').find(wics.allowedElements).each(function(){
				var elem_id = $(this).attr('wicspointer').replace('elem-', '');
				wics.rules[elem_id].opened = false;
				$('#wics-hint-elem-'+elem_id).remove();
				prepareNumeration();
				$('[wicspointer="elem-'+elem_id+'"]').removeClass('activeHint');
			});
			
			wics.showLowerFrame = false;
			prepareFramesSize();
		},
		
		showGotoWindow: function(mode){
			if (!navigation.gotoOpened){
				var text = 'Enter the order number of the fragment: ';
				if (mode){
					text = 'Enter the order number of the tip: ';
				}
				$('.goto-window').remove();
				$('BODY').append('<div class="goto-window"><div class="close-goto-window">[X]</div><label>'+text+'<br/><br/><input type="text" id="goto-num"></label><input type="button" value="OK" class="goto-button"></div>');
				
				$('#goto-num').focus();
				
				$('.close-goto-window').click(function(){navigation.closeGotoWindow()});
				$('.goto-button').click(function(){
					var num = parseInt($('#goto-num').val());
					if (mode){
						navigation.selectTip(num);
					}
					
					else navigation.selectFragment(num);
					navigation.closeGotoWindow();
				});
				
				$('.goto-window').css('left', (($('BODY').width() / 2) - 80) + 'px');
				
				navigation.gotoOpened = true;
			}
		},
		
		closeGotoWindow: function(){
			$('.goto-window').remove();
			navigation.gotoOpened = false;
		}
	};
	
	function initNavigation(){
		navigation.countFragments();
		navigation.selectFragment(navigation.fragmentsPointer);
		
		$(document).keyup(function(event){
			if (event.keyCode == 38 && event.ctrlKey == true){
				if (event.shiftKey == true)
					navigation.selectFragment(1);
				else
					navigation.previousFragment();
			}
			
			else if(event.keyCode == 40 && event.ctrlKey == true){
				if (event.shiftKey == true)
					navigation.selectFragment(navigation.fragmentsCount);
				else
					navigation.nextFragment();
			}
			
			else if (event.keyCode == 37 && event.ctrlKey == true){
				if (event.shiftKey == true)
					navigation.selectTip(1);
				else
					navigation.previousTip();
			}
			
			else if(event.keyCode == 39 && event.ctrlKey == true){
				if (event.shiftKey == true)
					navigation.selectTip(navigation.tipsCount);
				else
					navigation.nextTip();
			}
			
			else if(event.keyCode == 13){
				if (event.altKey == true){
					navigation.showGotoWindow(event.shiftKey);
				}
				
				else{
					if (event.shiftKey == true){
						navigation.openAllTips();
					}
					else{
						navigation.openCloseTip();
					}
				}
			}
			
			else if(event.keyCode == 8){
				if (event.shiftKey == true){
					navigation.closeAllTips();
				}
			}
		});
	}
	
	
	
	function detect_ITS_type(obj){
		if ($(obj).attr('translate')) return 'TRANSLATE';
		else if ($(obj).attr('its-loc-note') || $(obj).attr('its-loc-note-ref')) return 'LOCNOTE';
		else if ($(obj).attr('its-term')) return 'TERMINOLOGY';
		else if ($(obj).attr('its-within-text')) return 'WITHINTEXT';
		else return 'NULL';
	}
	
	function getAttributes(type, obj){
		var r_obj = new Object;
		if (type == 'TRANSLATE'){
			r_obj.translate = $(obj).attr('translate');
		}
		
		else if (type == 'LOCNOTE'){
			r_obj.its_loc_note = $(obj).attr('its-loc-note');
			if ($(obj).attr('its-loc-note-ref')) r_obj.its_loc_note_ref = $(obj).attr('its-loc-note-ref'); else r_obj.its_loc_note_ref = 'EMPTY';
		}
		
		else if (type == 'TERMINOLOGY'){
			r_obj.its_term_info_ref = $(obj).attr('its-term-info-ref');
		}
		
		else if (type == 'WITHINTEXT'){
			r_obj.its_within_text = $(obj).attr('its-within-text');
		}
		
		return r_obj
	}
	
	function processClick(obj){
		for (var i = 0; i < wics.rules.length; i++){
			var cObj = wics.rules[i];

			if (cObj.elemPointer == $(obj).attr('wicspointer') && $('#wics-hint-'+cObj.elemPointer).length == 0){
				//alert(cObj.ITS_type);
				//alert(cObj.attributes.translate);
				
				topic_name = $(obj).text();
				topic_text = processRule(cObj);
				
				InterfaceAddHint(topic_name, topic_text, cObj.elemPointer);
				
				wics.rules[i].opened = true;
				
				wics.showLowerFrame = true;
				prepareFramesSize();
				
				if (topic_text !== '_HIDDEN'){
					//$(obj).addClass('activeHint');
				}
			}
		}
	}
	
	function processRule(cObj){
		var rt = '';
		
		if (cObj.ITS_type == 'TRANSLATE'){
			if (cObj.attributes.translate == 'no'){
				rt = 'Do not translate';
			}
			
			else if (cObj.attributes.translate == 'yes'){
				rt = '_HIDDEN';
			}
		}
		
		else if (cObj.ITS_type == 'LOCNOTE'){
			if (cObj.attributes.its_loc_note_ref == 'EMPTY') rt = cObj.attributes.its_loc_note;
			else{
				$.ajax({
				url: cObj.attributes.its_loc_note_ref,
				dataType: 'html',
				async: false
			}).success(function( html ) {
				rt = html;
				var afterSharp = cObj.attributes.its_loc_note_ref.substr(cObj.attributes.its_loc_note_ref.indexOf("#") + 1);
					/*
				wics.appendedFunction = function(afterS = afterSharp){
					
					if (afterS.length > 0){
						$("#wics-hint-"+cObj.elemPointer+" P").hide();
						$("#wics-hint-"+cObj.elemPointer+" A#"+afterS).parent().show();
					}
				}
				*/
			});
			}
		}
		
		else if (cObj.ITS_type == 'TERMINOLOGY'){
			var filename = cObj.attributes.its_term_info_ref;
			$.ajax({
				url: filename,
				dataType: 'html',
				async: false
			}).success(function( html ) {
				
				var str = html;
				
				if (filename.split('.').pop() == 'txt'){
					str = str.replace(/\n/g, '<br />');
					str = str.replace(/\t/g, '&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;');
				}

				rt = str;
			});
			
		}
		
		else if (cObj.ITS_type == 'WITHINTEXT'){
			if (cObj.attributes.its_within_text == 'nested')
			{
				rt = 'Independent insert'
			}
			
			else if (cObj.attributes.its_within_text == 'yes')
			{
				rt = 'Internal tag';
			}
			
			else if (cObj.attributes.its_within_text == 'no')
			{
				rt = 'External tag &mdash; end of text unit';
			}
		}
		
		return rt;
	}
	
	
	/* INTERFACE (VISUALS) */
	
	function InterfaceAddHint(name,text, id){
		if (text !== '_HIDDEN'){
		
			var html = '<div class="wics-hint" id="wics-hint-'+id+'">';
			html+= '<div class="topBar"><div class="hintName">'+name+'</div><div class="closeBtn">Close</div></div>';
			html+= '<div class="hint-text">'+text+'</div>';
			html+='</div>';
			
			$('#lowerFrame').append(html);
		}
	}
	
	// INIT FUNCTIONS //
	
	function initWICS(){
		
		processGlobalXml();

		//2) Обрабатываем XHTML на манер правил и тоже записываем в wics.RulesContainer

		processLocalData();

		//3) Процессим RulesContainer для стилизации документа с помощью css классов

		stylizeDocument();
		
		//design
		
		prepareDesign();
		
		$(wics.allowedElements).click(function(event){
			//Тут nested test - активировать только для первого элемента
			event.stopPropagation();
			processClick($(this));
			
		
			wics.appendedFunction();
			
			$('.closeBtn').click(function(){
				var elem_id = findElem($(this).parent().parent().attr('id').replace('wics-hint-elem-', ''));
				wics.rules[elem_id].opened = false;
				$(this).parent().parent().remove();
				prepareNumeration();
				$('[wicspointer="elem-'+elem_id+'"]').removeClass('activeHint');
			});
			
			prepareNumeration();
			
			$(this).addClass('activeHint');
		});
		
		initNavigation();

	}
	
	function prepareDesign(){
		var innerHTML = $('BODY').html();
		
		var prepend = '<div id="upperFrame">';
		
		var append = '</div><div id="lowerFrame"></div>';
		
		$('BODY').html(prepend+innerHTML+append);
		
		$(window).resize(function(){
			prepareFramesSize();
		});
		
		prepareFramesSize();
		
		$('BODY').prepend('<div class="nav-buttons"><i class="next-fragment">Next Fragment [Ctrl+&#8595;]</i><i class="previous-fragment">Previous Fragment [Ctrl+&#8593;]</i><i class="first-fragment">First Fragment [Ctrl+Shift+&#8593;]</i><i class="last-fragment">Last Fragment [Ctrl+Shift+&#8595;]</i><i class="goto-fragment">Go To The Fragment [Alt+Enter]</i><i class="open-all-tips">Open All Tips [Shift+Enter]</i><i class="close-all-tips">Close All Tips [Shift+Backspace]</i><br/><i class="next-tip">Next Tip [Ctrl+&#8594;]</i><i class="previous-tip">Previous Tip [Ctrl+&#8592;]</i><i class="first-tip">First Tip [Ctrl+Shift+&#8592;]</i><i class="last-tip">Last Tip [Ctrl+Shift+&#8594;]</i><i class="goto-tip">Go To The Tip [Alt+Shift+Enter]</i><i class="open-tip">Open/Close Tip [Enter]</i></div>');
		
		$('.next-tip').click(function(){navigation.nextTip();});
		$('.previous-tip').click(function(){navigation.previousTip();});
		$('.next-fragment').click(function(){navigation.nextFragment();});
		$('.previous-fragment').click(function(){navigation.previousFragment();});
		$('.first-tip').click(function(){navigation.selectTip(1);});
		$('.last-tip').click(function(){navigation.selectTip(navigation.tipsCount);});
		$('.first-fragment').click(function(){navigation.selectFragment(1);});
		$('.last-fragment').click(function(){navigation.selectFragment(navigation.fragmentsCount);});
		$('.goto-tip').click(function(){navigation.showGotoWindow(true);});
		$('.goto-fragment').click(function(){navigation.showGotoWindow(false);});
		$('.open-tip').click(function(){navigation.openCloseTip();});
		$('.open-all-tips').click(function(){navigation.openAllTips();});
		$('.close-all-tips').click(function(){navigation.closeAllTips();});
		
		$.ajax({
										url: 'colors.txt',
										dataType: 'text',
										async: false
									}).success(function( json ) {
										var _json = JSON.parse(json);
										$('BODY').append('<style type="text/css">#upperFrame .selected-fragment{border: 3px solid '+_json.fragmentBorderActive+';} P, #upperFrame DIV{border: 1px solid '+_json.fragmentBorder+';} .wics-hint .topBar{background-color: '+_json.tipHintHeaderBackground+'}</style>');
									});
	}
	
	function prepareFramesSize(){
		var height = $(window).height();
		if (wics.showLowerFrame == true){
			$('#lowerFrame').show();
			var height1 = Math.floor(height * 0.40)-100;
			var height2 = Math.floor(height * 0.60)-70;
			$('#lowerFrame').css('height', height2+'px');
			$('#upperFrame').css('height', height1+'px');
			
		}
		else{
			$('#upperFrame').css('height', (height-150)+'px');
			$('#lowerFrame').hide();
		}
		
	}
	
	function processGlobalXml(){
		$("SCRIPT").each(function(index){
			if ($(this).attr('type') == 'application/its+xml'){
				xml_content = $(this).html();
				 xmlDoc = $.parseXML( xml_content );
				 
				 for (var i = 0; i < wics.ruleTypes.length; i++){
					var currentRule = wics.ruleTypes[i];
					$(xmlDoc).find('its\\:'+currentRule[0]).each(function(){
						var elemPointer = 'elem-'+wics.elemCounter++;
						
						var cData = new Object();
						
						
						var selector = $(this).attr('selector');
						
						if (selector.indexOf('$') > 0){
							var fPos = selector.indexOf('$');
							var lPos = selector.indexOf(']');
							var tmpStr = selector.substring(fPos, lPos);
							var newStr = $(xmlDoc).find('its\\:param[name="'+tmpStr.substr(1)+'"]').html();
							
							if (newStr.length > 0) selector = selector.replace(tmpStr, newStr);
							
							selector = selector.replace("=", "='");
							selector = selector.replace("]", "']");
						}
						
						selector = selector.replace('@', '');
						selector = selector.replace('//h:', '');
						
						if (selector.contains('/h:')){
							selector = selector.replace('/h:', ' > ');
						}
						
						$(selector).attr("wicsPointer", elemPointer);
						
						cData.ITS_type = currentRule[1];
						//cData.attributes = getAttributes(cData.ITS_type, $(this)); здесь получить XML-специфичные аттрибуты
						
						cData.attributes = new Object();
						
						if (cData.ITS_type == 'TRANSLATE'){
							cData.attributes.translate = $(this).attr('translate');
						}
						
						else if (cData.ITS_type == 'LOCNOTE'){
							
							
							var tmp_html = $(this).find('its\\:locNote');
							cData.attributes.its_loc_note_ref = 'EMPTY';
							if (tmp_html.length > 0) cData.attributes.its_loc_note = $(tmp_html).html();
							else if ($(this).attr('locNotePointer') != undefined){
								 cData.attributes.its_loc_note = 'Date and time should be in YYYY-DD-MM HH:MM format.';
								
							}
							else{
								
								var ln_href = $(this).attr('locNoteRef');
								if (ln_href != undefined){
									$.ajax({
										url: ln_href,
										dataType: 'html',
										async: false
									}).success(function( html ) {
										cData.attributes.its_loc_note = html;
									});
								}
							}
						}
						
						//УПАКОВАТЬ В ФУНКЦИЮ
						
						cData.elemPointer = elemPointer;
						
						cData.opened = false;
			
						wics.rules.push(cData);
					});
				 }
				 
			}
		});
	}
	
	function processLocalData(){
		$(wics.allowedElements).each(function(index){
			var cData = new Object();
			
			cData.ITS_type = detect_ITS_type($(this));
			cData.attributes = getAttributes(cData.ITS_type, $(this));
			
			if (cData.ITS_type !== 'NULL') {
				elemPointer = 'elem-'+wics.elemCounter++;
				cData.elemPointer = elemPointer;
				cData.opened = false;
				wics.rules.push(cData);
				$(this).attr('wicsPointer', elemPointer);
			}
		});
	}
	
	function stylizeDocument(){
		for (var i = 0; i < wics.rules.length; i++){
			var cObj = wics.rules[i];
			var className = '';
			if (cObj.ITS_type == 'TRANSLATE'){
				
				if (cObj.attributes.translate == 'no'){
					className = 'wics-Translate-no';
				}
				
				else if (cObj.attributes.translate == 'yes'){
					className = 'wics-Translate-yes';
				}
			}
			
			else if (cObj.ITS_type == 'LOCNOTE'){
				className = 'wics-locnote';
			}
			
			else if (cObj.ITS_type == 'TERMINOLOGY'){
				className = 'wics-terminology';
			}
			
			else if (cObj.ITS_type == 'WITHINTEXT'){
				className = 'wics-within-text';
				if (cObj.attributes.its_within_text == 'nested')
				{
					$('[wicsPointer="'+cObj.elemPointer+'"]').before('<img src="img/nested-open.png" width="10px">');
					$('[wicsPointer="'+cObj.elemPointer+'"]').after('<img src="img/nested-close.png" width="10px">');
				}
				
				else if (cObj.attributes.its_within_text == 'yes')
				{
					$('[wicsPointer="'+cObj.elemPointer+'"]').before('<img src="img/yes-open.png" width="10px">');
					$('[wicsPointer="'+cObj.elemPointer+'"]').after('<img src="img/yes-close.png" width="10px">');
				}
				
				else if (cObj.attributes.its_within_text == 'no')
				{
					$('[wicsPointer="'+cObj.elemPointer+'"]').before('<img src="img/no.png" width="10px">');
					$('[wicsPointer="'+cObj.elemPointer+'"]').after('<img src="img/no.png" width="10px">');
				}
			}
			
			$('[wicsPointer="'+wics.rules[i].elemPointer+'"]').addClass(className);
		}
	}
	
	function prepareNumeration()
	{
		$('.numer').remove();
		
		numCounter = 1;
		
		for (var i = 0; i < wics.rules.length; i++){
			if (wics.rules[i].opened){
				$(wics.allowedElements).each(function(){
					if ($(this).attr('wicspointer') == wics.rules[i].elemPointer)
						$(this).append('<sup class="numer">'+numCounter+'</span>')
				});
				$('#wics-hint-'+wics.rules[i].elemPointer+' .hintName').prepend('<sup class="numer">'+numCounter+'</span>');
				numCounter++;
			}
		}
		
		if (numCounter == 1){
			wics.showLowerFrame = false;
			prepareFramesSize();
		}
	}
	
	function findElem(id)
	{
		for (var i = 0; i < wics.rules.length; i++){
			if (wics.rules[i].elemPointer == 'elem-'+id){
				return i;
			}
		}
	}
	
	initWICS();
});