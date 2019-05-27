// ------- Propeller Checkbox component js function ------- //
var pmdNavbar = function ($) {
    /**
     * ------------------------------------------------------------------------
     * Constants
     * ------------------------------------------------------------------------
     */
    var NAME = 'pmdNavbar';
    var VERSION = '1.0.0';
    var JQUERY_NO_CONFLICT = $.fn[NAME];
    var isOpenWidth = 1200;

    var ClassName = {
        OPEN: 'pmd-sidebar-open',
        OVERLAY_ACTIVE: 'pmd-sidebar-overlay-active',
        BODY_OPEN: 'pmd-body-open',
        NAVBAR_SIDEBAR: 'pmd-navbar-sidebar'
    }

    var Selector = {
        BODY:'body',
        PARENT_SELECTOR: '',
        OVERLAY:'.pmd-sidebar-overlay',
        NAVBAR_SIDEBAR: '.' + ClassName.NAVBAR_SIDEBAR,
		NAVBAR_TOGGLE: '.pmd-navbar-toggle'
    };

    var Event = {
        CLICK: 'click'
    }

 	// Nave bar in Sidebar
    function onNavBarToggle(e) {
        $(Selector.NAVBAR_SIDEBAR).toggleClass(ClassName.OPEN);
        if (($(Selector.NAVBAR_SIDEBAR).hasClass(ClassName.NAVBAR_SIDEBAR)) && $(Selector.NAVBAR_SIDEBAR).hasClass(ClassName.OPEN)) {
            $(Selector.OVERLAY).addClass(ClassName.OVERLAY_ACTIVE);
            $(Selector.BODY).addClass(ClassName.BODY_OPEN)
        } else {
            $(Selector.OVERLAY).removeClass(ClassName.OVERLAY_ACTIVE);
            $(Selector.BODY).addClass(ClassName.BODY_OPEN)
        }
    }
	
	// Overlay
    function onOverlayClick(event) {
        var $this = $(event.currentTarget);
        $this.removeClass(ClassName.OVERLAY_ACTIVE);
        $(Selector.SIDEBAR).removeClass(ClassName.OPEN);
        $(Selector.NAVBAR_SIDEBAR).removeClass(ClassName.OPEN);
        $(Selector.BODY).removeClass(ClassName.BODY_OPEN)
        event.stopPropagation();
    }

	var pmdNavbar = function () {
        _inherits(pmdNavbar, commons);
        function pmdNavbar(options) {
            $(pmdNavbar.prototype.attachParentSelector(Selector.PARENT_SELECTOR, Selector.NAVBAR_TOGGLE)).off(Event.CLICK);
			$(pmdNavbar.prototype.attachParentSelector(Selector.PARENT_SELECTOR, Selector.NAVBAR_TOGGLE)).on(Event.CLICK, onNavBarToggle);
			$(pmdNavbar.prototype.attachParentSelector(Selector.PARENT_SELECTOR, Selector.OVERLAY)).off(Event.CLICK);
            $(pmdNavbar.prototype.attachParentSelector(Selector.PARENT_SELECTOR, Selector.OVERLAY)).on(Event.CLICK, onOverlayClick);
        }
        return pmdNavbar;
    }()

    /**
     * ------------------------------------------------------------------------
     * jQuery
     * ------------------------------------------------------------------------
     */
    var plugInFunction = function (arg) {
        if (this.selector !== "") {
            Selector.PARENT_SELECTOR = this.selector;
        }
        new pmdNavbar(arg);
    }

    $.fn[NAME] = plugInFunction;

    return pmdNavbar;

} (jQuery)()
