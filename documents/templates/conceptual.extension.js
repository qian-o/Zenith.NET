const resources = require('./resources.common.js');
exports.postTransform = function (model) {
    const strings = resources.prepare(model);
    const templates = [];
    model._isLanding = model._layout === 'landing';
    model.rawTitle = resources.bind(model.rawTitle || '', strings, templates);
    model.conceptual = resources.bind(model.conceptual, strings, templates);
    model._resourceTemplates = templates.join('');
    return model;
};
