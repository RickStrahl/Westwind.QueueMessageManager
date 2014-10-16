(function () {
    'use strict';
    

    var app = angular.module('app', [
        // Angular modules 
        'ngSanitize'

        // Custom modules 

        // 3rd Party Modules
    ]);
app.config([              
            function() {
                console.log('app started.');
            }
        ])
        .filter('linebreakFilter', function() {
            return function(text) {
                if (text !== undefined)
                    return text.replace(/\n/g, '<br />');
                return text;
            };
        });    
})();