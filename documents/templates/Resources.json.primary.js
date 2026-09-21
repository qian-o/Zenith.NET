const resources = require('./resources.common.js');
exports.getOptions = () => ({ isShared: true });
exports.transform = model => ({ content: JSON.stringify(resources.validate(model)) });
