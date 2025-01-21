var Interactable = pc.createScript('interactable');

Interactable.attributes.add('panelName', {type: 'string',});
Interactable.attributes.add('isExternalLink', {type: 'boolean',});
Interactable.attributes.add('interactionMessage', {type: 'string',});

Interactable.attributes.add('DetailsText', {
    type: 'entity',
    description: 'Reference to the text on top of the arcade machine.'
});

// initialize code called once per entity
Interactable.prototype.initialize = function() {
    // Initialization code here
};