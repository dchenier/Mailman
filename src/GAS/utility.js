

/**
 * Ensures the assigned trigger is valid.
 *
 * @param  {Trigger} trigger The Trigger to test.
 * @return {boolean} True if the Trigger is valid, false otherwise.
 */
function validateTrigger(trigger) {
  if (trigger.getEventType() !== ScriptApp.EventType.CLOCK) {
    log('Invalid trigger event type');
    return false;
  }
  if (trigger.getHandlerFunction() !== 'sendManyEmails') {
    log('Invalid trigger function');
    return false;
  }

  return true;
}


/**
 * Validates all of Mailman's triggers. Logs any issues for debug purposes.
 *
 * @return {boolean} True if the triggers are set up properly, false if they are set up incorrectly.
 */
function validateTriggers() {
  SPREADSHEET_ID = PropertiesService.getDocumentProperties().getProperty(PROPERTY_SS_ID);
  var ss = SpreadsheetApp.openById(SPREADSHEET_ID);

  var triggers = ScriptApp.getUserTriggers(ss);
  log('Triggers:' + triggers.length);
  if (triggers.length !== 1) {
    log('Incorrect number of triggers: ' + triggers.length);
    return false;
  }

  return validateTrigger(triggers[0]);
}


/**
 * This gets the values of the top-most row.
 *
 * @param {Sheet} sheet The sheet to find the headers in.
 * @return {Array<string>} The array of values.
 */
function getHeaderStrings(sheet) {
  var row = parseInt(getHeaderRow());

  return getValues(sheet, row - 1);
}


/**
 * Gets an array of the values in a specific sheets row.
 *
 * @param {Sheet} sheet The sheet to get the values from.
 * @param {number} rowIndex The zero-based index of the row to retrieve values for.
 * @return {Array<string>} The array of values.
 */
function getValues(sheet, rowIndex) {
  var range = sheet.getDataRange();

  var row = range.offset(rowIndex, 0, 1, range.getNumColumns());

  var values = [];
  for (var i = 1; i <= row.getNumColumns(); i++) {
    values.push(row.getCell(1, i).getValue());
  }

  return values;
}


/**
 * This function gets a value from a specific Sheet, column and row.
 * The column is specified by header name. TODO Test me
 *
 * @param {Sheet} sheet The Sheet to find the value in.
 * @param {String} headerName The name of the header. This determines the column to look in.
 * @param {Number} row The 0-based row index. 0 is the very top row in the Sheet.
 * @return {String} The string value found in the given row/column/Sheet.
 */
function getValue(sheet, headerName, row) {
  var headerStrings = getHeaderStrings(sheet);
  var column = headerStrings.indexOf(headerName);

  if (column === -1) {
    return null;
  }

  return getValues(sheet, row)[column];
}


/**
 * This function gets a value from a specific Sheet, column and row.
 * The column is specified by header name. TODO Test me
 *
 * @param {Sheet} sheet The Sheet to find the value in.
 * @param {String} headerName The name of the header. This determines the column to look in.
 * @param {Number} row The 0-based row index. 0 is the very top row in the Sheet.
 * @return {String} The string value found in the given row/column/Sheet.
 */
function getCell(sheet, headerName, row) {
  var headerStrings = getHeaderStrings(sheet);
  var column = headerStrings.indexOf(headerName);

  if (column === -1) {
    return null;
  }

  return sheet.getDataRange().getCell(row + 1, column + 1);
}


/**
 * This function replaces  all instances of <<tags>> with the data in headerToData.
 *
 * @param {string} text The string that contains the tags.
 * @param {Object} headerToData A key-value pair where the key is a column name
 * and the value is the data in the column.
 * @return {string} The text with all tags replaced with data.
 */
function replaceTags(text, headerToData) {
  var dataText = text.replace(/<<.*?>>/g, function(match, offset, string) {
    var columnName = match.slice(2, match.length - 2);
    return headerToData[columnName];
  });

  return dataText;
}


/**
 * Get the rule for this document.
 *
 * @return {object} The rule in object form.
 */
function getRules() {
  return JSON.parse(load(RULE_KEY));
}


/**
 * Source: http://stackoverflow.com/questions/21229180/convert-column-index-into-corresponding-column-letter
 * Converts a column index into the column letter.
 *
 * @param {number} column  The column index
 * @return {string}  The column letters
 */
function columnToLetter(column) {
  var temp, letter = '';
  while (column > 0) {
    temp = (column - 1) % 26;
    letter = String.fromCharCode(temp + 65) + letter;
    column = (column - temp - 1) / 26;
  }
  return letter;
}


/**
 * Deletes all triggers associated with the given Sheet.
 *
 * @param  {Sheet} sheet The Sheet to remove triggers from.
 */
function deleteAllTriggers(sheet) {
  var triggers = ScriptApp.getUserTriggers(sheet);

  for (var i = 0; i < triggers.length; i++) {
    ScriptApp.deleteTrigger(triggers[i]);
  }
}


/**
 * Removes the triggers for the Sheet the add on is installed in.
 * 
 */
function deleteForThisSheet() {
  SPREADSHEET_ID = PropertiesService.getDocumentProperties().getProperty(PROPERTY_SS_ID);
  var ss = SpreadsheetApp.openById(SPREADSHEET_ID);

  deleteAllTriggers(ss);
}


/**
 * Logs the Documents properties. Used for testing purposes.
 *
 */
function checkDocumentProperties() {
  Logger.log(PropertiesService.getDocumentProperties().getProperties());
}


/**
 * Cleans all properties. We need the sheet id stored as a property. Remember to get this id again.
 *
 */
function removeProperties() {
  PropertiesService.getDocumentProperties().deleteAllProperties();
}
