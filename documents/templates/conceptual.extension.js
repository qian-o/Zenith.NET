const resources = require('./resources.common.js');
exports.postTransform = function (model) {
    const strings = resources.prepare(model);
    model.rawTitle = resources.bind(model.rawTitle || '', strings);
    model.conceptual = resources.bind(model.conceptual, strings);
    return model;
};
