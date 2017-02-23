
var baseHTML = require('./merge-templates-list-view.html');
var MergeTemplateListItem = require('./merge-template-list-item.js');
var MergeTemplate = require('../../data/merge-template/merge-template.js');
var MergeTemplateContainer = require('../../data/merge-template-container.js');
var Util = require('../../util.js');
var PubSub = require('pubsub-js');
var ActionBar = require('../../action-bar/action-bar.js');



/**
 * This view displays all of the MergeTemplates. Each MergeTemplate corresponds to a MergeTemplateListItem.
 * This view responds to the following PubSub events: Rules.delete, Rules.add, Rules.update.
 * This view publishes the following events: Mailman.RulesListView.show.
 * @constructor
 * @param {jquery} appendTo The element this view should be appended to.
 */
var MergeTemplatesListView = function(appendTo) {
  // private variables
  var self = this;
  var base = $(baseHTML);
  var listItems = [];
  var mergeTemplates;
  var actionBar = ActionBar;

  // jQuery Objects
  var list = base.find('[data-id="list"]');
  var emptyContainer = base.find('[data-id="empty-container"]');
  var triggerButton = base.find('[data-id="trigger-button"]');
  var instantButton = base.find('[data-id="instant-button"]');

  // Event callbacks
  var deletionCallback;
  var editCallback;
  var runCallback;
  var triggerCB;
  var instantCB;

  // public variables


  //***** private methods *****//

  this.init_ = function(appendTo) {
    appendTo.append(base);

    triggerButton.on('click', newTrigger);
    instantButton.on('click', newInstant);

    PubSub.subscribe('Rules.delete', rebuild);
    PubSub.subscribe('Rules.add', rebuild);
    PubSub.subscribe('Rules.update', rebuild);
    PubSub.subscribe('Mailman.SettingsView.hide', self.show);

    componentHandler.upgradeElement(triggerButton[0], 'MaterialButton');
    componentHandler.upgradeElement(instantButton[0], 'MaterialButton');
  };

  var itemDelete = function(e) {
    deletionCallback(e.data);
  };

  var itemEdit = function(e) {
    editCallback(e.data);
  };

  var itemRun = function(e) {
    runCallback(e.data);
  };

  var newTrigger = function(e) {
    triggerCB(e);
  };

  var newInstant = function(e) {
    instantCB(e);
  };

  var rebuild = function() {
    for (var i = 0; i < listItems.length; i++) {
      listItems[i].cleanup();
    }

    listItems = [];
    for (var i = 0; i < mergeTemplates.length(); i++) {
      self.add(mergeTemplates.get(i));
    }

    setEmptyDisplay();
  };

  var setEmptyDisplay = function() {
    if (listItems.length === 0) {
      Util.setHidden(list, true);
      Util.setHidden(emptyContainer, false);
      actionBar.hideBranding();
    }
    else {
      Util.setHidden(list, false);
      Util.setHidden(emptyContainer, true);
      actionBar.showBranding();
    }
  };

  //***** public methods *****//

  /**
   * Sets the MergeTemplateContainer this view uses.
   *
   * @param {MergeTemplateContainer} container This is the model used by the view to update.
   */
  this.setContainer = function(container) {
    mergeTemplates = container;
    rebuild();
  };

  /**
   * Adds a new MergeTemplateListItem to this view.
   *
   * @param {MergeTemplate} template The model that is used to build the view.
   */
  this.add = function(template) {

    var item = new MergeTemplateListItem(list, template);
    item.setDeleteHandler(itemDelete);
    item.setEditHandler(itemEdit);
    item.setRunHandler(itemRun);

    listItems.push(item);
  };

  /**
   * Hides this view.
   *
   */
  this.hide = function() {
    Util.setHidden(base, true);
    actionBar.showBranding();
  };

  /**
   * Shows this view.
   * TODO
   */
  this.show = function() {
    setEmptyDisplay();
    Util.setHidden(base, false);
    PubSub.publish('Mailman.RulesListView.show');
  };

  /**
   * Sets the handler for each list item deletion.
   *
   * @param {Function} callback Called when the delete icon is clicked.
   */
  this.setDeleteHandler = function(callback) {
    deletionCallback = callback;
  };

  /**
   * Sets the handler for each list item edit.
   *
   * @param {Function} callback Called when the edit icon is clicked.
   */
  this.setEditHandler = function(callback) {
    editCallback = callback;
  };

  /**
   * Sets the handler for each list item run.
   *
   * @param {Function} callback Called when the run icon is clicked.
   */
  this.setRunHandler = function(callback) {
    runCallback = callback;
  };

  /**
   * Sets the handler for the new trigger button click.
   * TODO
   * @param {Function} callback Called when the add trigger button is clicked.
   */
  this.setTriggerHandler = function(callback) {
    triggerCB = callback;
  };

  /**
   * Sets the handler for the instant email button click.
   * TODO
   * @param {Function} callback Called when the instant trigger button is clicked.
   */
  this.setInstantHandler = function(callback) {
    instantCB = callback;
  };

  this.init_(appendTo);
};


/** */
module.exports = MergeTemplatesListView;
